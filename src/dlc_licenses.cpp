// Portions copyright (c) 2026, Pinyon Shift contributors (BSD 3-Clause: see THIRD_PARTY_NOTICES.md).
// Copyright (c) 2026, i2kiselev (BSD 3-Clause: see LICENSE in the repository root).
#include "dlc_licenses.h"

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstring>
#include <fstream>
#include <string_view>
#include <system_error>

#include <rex/filesystem.h>
#include <rex/logging.h>
#include <rex/system/xam/content_manager.h>

namespace fm4_static_recomp {

DlcLicenseRepairResult RepairInstalledDlcLicenses(const std::filesystem::path& user_root) {
  using rex::system::xam::XCONTENT_AGGREGATE_DATA;
  constexpr size_t kMetadataSize = sizeof(XCONTENT_AGGREGATE_DATA);
  constexpr std::array<uint8_t, 4> kFullLicense{0xFF, 0xFF, 0xFF, 0xFF};
  const auto title = rex::filesystem::ExtendedLengthPath(
      user_root / "0000000000000000" / "4D530910");
  const auto packages = title / "00000002";
  const auto headers = title / "Headers" / "00000002";
  DlcLicenseRepairResult result;
  auto fail = [&](const std::filesystem::path& path, std::string_view reason) {
    REXLOG_ERROR("Content: DLC license repair failed for {}: {}", rex::path_to_utf8(path), reason);
    ++result.failed;
  };

  std::error_code scan_error;
  if (!std::filesystem::exists(packages, scan_error)) {
    if (scan_error) fail(packages, scan_error.message());
    return result;
  }
  for (std::filesystem::directory_iterator it(packages, scan_error), end;
       !scan_error && it != end; it.increment(scan_error)) {
    std::error_code error;
    if (!it->is_directory(error)) {
      if (error) fail(it->path(), error.message());
      continue;
    }
    const auto name = rex::path_to_utf8(it->path().filename());
    auto path = headers / it->path().filename();
    path += ".header";
    const bool exists = std::filesystem::exists(path, error);
    if (error) {
      fail(path, error.message());
      continue;
    }
    std::array<uint8_t, kMetadataSize + kFullLicense.size()> bytes{};
    if (exists) {
      const auto size = std::filesystem::file_size(path, error);
      if (error || size < kMetadataSize) {
        fail(path, error ? error.message() : "invalid installed header size");
        continue;
      }
      std::ifstream input(path, std::ios::binary);
      if (!input.read(reinterpret_cast<char*>(bytes.data()),
                      std::streamsize(std::min<uintmax_t>(size, bytes.size())))) {
        fail(path, "cannot read installed header");
        continue;
      }
      XCONTENT_AGGREGATE_DATA data;
      std::memcpy(&data, bytes.data(), sizeof(data));
      if (data.title_id != 0x4D530910 ||
          data.content_type != rex::system::XContentType::kMarketplaceContent) {
        fail(path, "not an FM4 marketplace header");
        continue;
      }
      if (size >= bytes.size() &&
          std::equal(kFullLicense.begin(), kFullLicense.end(), bytes.begin() + kMetadataSize)) {
        continue;
      }
    } else {
      XCONTENT_AGGREGATE_DATA data{};
      if (name.size() > sizeof(data.file_name_raw)) {
        fail(path, "package name exceeds the installed header format");
        continue;
      }
      data.device_id = 1;
      data.content_type = rex::system::XContentType::kMarketplaceContent;
      data.set_display_name(rex::path_to_utf16(it->path().filename()));
      data.set_file_name(name);
      data.title_id = 0x4D530910;
      data.xuid = 0;
      std::memcpy(bytes.data(), &data, sizeof(data));
      std::filesystem::create_directories(headers, error);
      if (error) {
        fail(headers, error.message());
        continue;
      }
    }
    std::copy(kFullLicense.begin(), kFullLicense.end(), bytes.begin() + kMetadataSize);
    const auto mode = exists ? std::ios::in | std::ios::out : std::ios::out | std::ios::noreplace;
    std::fstream output(path, std::ios::binary | mode);
    if (!output) {
      fail(path, "cannot open installed header for repair");
      continue;
    }
    const size_t offset = exists ? kMetadataSize : 0;
    output.seekp(offset);
    output.write(reinterpret_cast<const char*>(bytes.data() + offset), bytes.size() - offset);
    output.flush();
    output.close();
    if (!output) {
      fail(path, "cannot write installed license mask");
      if (!exists) std::filesystem::remove(path, error);
      continue;
    }
    REXLOG_INFO("Content: unlocked installed package {}", name);
    ++result.repaired;
  }
  if (scan_error) fail(packages, scan_error.message());
  return result;
}

}  // namespace fm4_static_recomp
