// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
#include <catch2/catch_test_macros.hpp>

#include <chrono>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <string>

#include <rex/system/xam/content_manager.h>

#include "dlc_licenses.h"

namespace {

struct Installation {
  std::filesystem::path root = std::filesystem::temp_directory_path() /
      ("fm4-dlc-licenses-" + std::to_string(
          std::chrono::steady_clock::now().time_since_epoch().count()));
  std::filesystem::path title = root / "0000000000000000/4D530910";
  std::filesystem::path header = title / "Headers/00000002/PACK.header";
  std::filesystem::path payload = title / "00000002/PACK/Media/cars.bin";

  Installation() {
    std::filesystem::create_directories(header.parent_path());
    std::filesystem::create_directories(payload.parent_path());
    std::ofstream(payload, std::ios::binary) << "unchanged car payload";
  }
  ~Installation() {
    std::error_code error;
    std::filesystem::permissions(header, std::filesystem::perms::owner_write,
                                 std::filesystem::perm_options::add, error);
    std::filesystem::remove_all(root, error);
  }

  std::string WriteHeader(std::string trailer, uint32_t title_id = 0x4D530910,
                          uint32_t type = 2) {
    rex::system::xam::XCONTENT_AGGREGATE_DATA data{};
    data.device_id = 1;
    data.content_type = rex::system::XContentType(type);
    data.title_id = title_id;
    data.xuid = 0;
    data.set_display_name(u"Installed car pack");
    data.set_file_name("PACK");
    std::string bytes(reinterpret_cast<const char*>(&data), sizeof(data));
    bytes += trailer;
    std::ofstream(header, std::ios::binary).write(bytes.data(), bytes.size());
    return bytes;
  }
};

std::string Read(const std::filesystem::path& path) {
  std::ifstream input(path, std::ios::binary);
  return {std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>()};
}

}  // namespace

TEST_CASE("Installed DLC is unlocked without an original package", "[dlc]") {
  Installation install;
  std::string trailer;
  SECTION("missing license trailer") { trailer = ""; }
  SECTION("zero mask") { trailer = std::string(4, '\0'); }
  SECTION("partial mask") { trailer = std::string("\x02\0\0\0", 4); }
  SECTION("incomplete trailer") { trailer = std::string(2, '\0'); }
  const auto before = install.WriteHeader(trailer);

  const auto result = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);

  CHECK(result.repaired == 1);
  CHECK(result.failed == 0);
  CHECK(Read(install.header) == before.substr(0, 0x148) + std::string(4, '\xff'));
  CHECK(Read(install.payload) == "unchanged car payload");
}

TEST_CASE("Fully unlocked DLC is not rewritten", "[dlc]") {
  Installation install;
  const auto before = install.WriteHeader(std::string(4, '\xff'));
  const auto timestamp = std::filesystem::file_time_type::clock::now() - std::chrono::hours(24);
  std::filesystem::last_write_time(install.header, timestamp);

  const auto result = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);

  CHECK(result.repaired == 0);
  CHECK(result.failed == 0);
  CHECK(Read(install.header) == before);
  CHECK((std::filesystem::last_write_time(install.header) == timestamp));
}

TEST_CASE("DLC repair leaves saves and other titles alone", "[dlc]") {
  Installation install;
  SECTION("save type") { install.WriteHeader("", 0x4D530910, 1); }
  SECTION("another title") { install.WriteHeader("", 0x12345678); }
  const auto before = Read(install.header);

  const auto result = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);

  CHECK(result.repaired == 0);
  CHECK(result.failed == 1);
  CHECK(Read(install.header) == before);
}

TEST_CASE("Malformed installed metadata is reported and preserved", "[dlc]") {
  Installation install;
  SECTION("truncated aggregate") { std::ofstream(install.header) << "short"; }
  SECTION("unknown header format") { std::ofstream(install.header) << std::string(500, 'x'); }
  const auto before = Read(install.header);

  const auto result = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);

  CHECK(result.repaired == 0);
  CHECK(result.failed == 1);
  CHECK(Read(install.header) == before);
}

TEST_CASE("DLC repair preserves trailing data accepted by the runtime", "[dlc]") {
  Installation install;
  const auto before = install.WriteHeader(std::string(4, '\0') + "retained extension");

  const auto result = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);

  CHECK(result.repaired == 1);
  CHECK(result.failed == 0);
  CHECK(Read(install.header) == before.substr(0, 0x148) + std::string(4, '\xff') +
                               "retained extension");
  const auto timestamp = std::filesystem::last_write_time(install.header);
  const auto again = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);
  CHECK(again.repaired == 0);
  CHECK(again.failed == 0);
  CHECK((std::filesystem::last_write_time(install.header) == timestamp));
}

TEST_CASE("Installed DLC without an enumeration header gets a full license", "[dlc]") {
  Installation install;
  SECTION("header file missing") {}
  SECTION("header directory missing") { std::filesystem::remove(install.header.parent_path()); }

  const auto result = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);

  REQUIRE(result.repaired == 1);
  CHECK(result.failed == 0);
  const auto bytes = Read(install.header);
  REQUIRE(bytes.size() == 0x14C);
  CHECK(bytes.substr(0, 8) == std::string("\0\0\0\x01\0\0\0\x02", 8));
  CHECK(bytes.substr(0x108, 5) == std::string("PACK\0", 5));
  CHECK(bytes.substr(0x134, 8) == std::string(8, '\0'));
  CHECK(bytes.substr(0x140, 4) == std::string("\x4D\x53\x09\x10", 4));
  CHECK(bytes.substr(0x148) == std::string(4, '\xff'));
  CHECK(Read(install.payload) == "unchanged car payload");
  CHECK(fm4_static_recomp::RepairInstalledDlcLicenses(install.root).repaired == 0);
}

TEST_CASE("An unfinished install does not receive a reconstructed header", "[dlc]") {
  Installation install;
  const auto marker = fm4_static_recomp::PendingInstallMarker(install.root, "PACK");
  CHECK(marker.filename() == "PACK.installing");
  std::ofstream(marker, std::ios::binary).flush();

  const auto pending = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);

  CHECK(pending.repaired == 0);
  CHECK(pending.failed == 0);
  CHECK_FALSE(std::filesystem::exists(install.header));
  CHECK(Read(install.payload) == "unchanged car payload");
  std::filesystem::remove(marker);
  CHECK(fm4_static_recomp::RepairInstalledDlcLicenses(install.root).repaired == 1);
}

TEST_CASE("An install marker does not block repair of an existing header", "[dlc]") {
  Installation install;
  const auto before = install.WriteHeader("");
  std::ofstream(fm4_static_recomp::PendingInstallMarker(install.root, "PACK"), std::ios::binary)
      .flush();

  const auto result = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);

  CHECK(result.repaired == 1);
  CHECK(Read(install.header) == before.substr(0, 0x148) + std::string(4, '\xff'));
}

TEST_CASE("A reconstructed header must preserve the complete package filename", "[dlc]") {
  Installation install;
  size_t length = 42;
  SECTION("maximum supported filename") { length = 42; }
  SECTION("filename would be truncated") { length = 43; }
  const std::string name(length, 'a');
  std::filesystem::rename(install.payload.parent_path().parent_path(),
                          install.title / "00000002" / name);
  const auto header = install.header.parent_path() / (name + ".header");

  const auto result = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);

  if (length == 42) {
    REQUIRE(result.repaired == 1);
    CHECK(result.failed == 0);
    CHECK(Read(header).substr(0x108, 42) == name);
  } else {
    CHECK(result.repaired == 0);
    CHECK(result.failed == 1);
    CHECK_FALSE(std::filesystem::exists(header));
  }
}

TEST_CASE("A blocked header directory is reported without replacing it", "[dlc]") {
  Installation install;
  const auto blocked = install.header.parent_path();
  std::filesystem::remove(blocked);
  std::ofstream(blocked, std::ios::binary) << "unrelated existing file";

  const auto result = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);

  CHECK(result.repaired == 0);
  CHECK(result.failed == 1);
  CHECK(Read(blocked) == "unrelated existing file");
  CHECK(Read(install.payload) == "unchanged car payload");
}

TEST_CASE("Orphan headers and unrelated files are not changed", "[dlc]") {
  Installation install;
  const auto before = install.WriteHeader("");
  size_t expected_repairs = 0;
  SECTION("no payload directory") {
    std::filesystem::rename(install.payload.parent_path().parent_path(),
                            install.title / "uninstalled-PACK");
  }
  SECTION("backup extension") {
    std::filesystem::rename(install.header, install.header.string() + ".backup");
    install.header += ".backup";
    expected_repairs = 1;
  }
  const auto result = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);
  CHECK(result.repaired == expected_repairs);
  CHECK(result.failed == 0);
  CHECK(Read(install.header) == before);
}

TEST_CASE("A user without installed content requires no repair", "[dlc]") {
  Installation install;
  const auto result = fm4_static_recomp::RepairInstalledDlcLicenses(install.root / "empty");
  CHECK(result.repaired == 0);
  CHECK(result.failed == 0);
}

TEST_CASE("The license scan is limited to the common marketplace folder", "[dlc]") {
  Installation install;
  install.WriteHeader("");
  const auto save = install.root / "B13EBABEBABEBABE/4D530910/Headers/00000001/save.header";
  std::filesystem::create_directories(save.parent_path());
  std::ofstream(save, std::ios::binary) << "saved game metadata";
  const auto upper = install.header.parent_path() / "PACK.HEADER";
  std::filesystem::rename(install.header, upper);
  install.header = upper;

  const auto result = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);

  CHECK(result.repaired == 1);
  CHECK(result.failed == 0);
  CHECK(Read(save) == "saved game metadata");
}

#if defined(_WIN32)
TEST_CASE("Read-only DLC headers are preserved and failures are reported", "[dlc]") {
  Installation install;
  bool full = false;
  SECTION("repair cannot write") { full = false; }
  SECTION("full mask needs no write access") { full = true; }
  const auto before = install.WriteHeader(full ? std::string(4, '\xff') : std::string());
  std::filesystem::permissions(install.header, std::filesystem::perms::owner_read);

  const auto result = fm4_static_recomp::RepairInstalledDlcLicenses(install.root);

  CHECK(result.repaired == 0);
  CHECK(result.failed == (full ? 0 : 1));
  CHECK(Read(install.header) == before);
}
#endif
