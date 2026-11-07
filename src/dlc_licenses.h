// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
#pragma once

#include <cstddef>
#include <filesystem>

namespace fm4_static_recomp {

struct DlcLicenseRepairResult {
  size_t repaired = 0;
  size_t failed = 0;
};

// Present from before a package's files are extracted until its header is written, so the
// repair does not mistake an interrupted install for a complete package without a header.
std::filesystem::path PendingInstallMarker(const std::filesystem::path& user_root,
                                           const std::filesystem::path& package);

DlcLicenseRepairResult RepairInstalledDlcLicenses(const std::filesystem::path& user_root);

}  // namespace fm4_static_recomp
