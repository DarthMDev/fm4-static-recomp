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

DlcLicenseRepairResult RepairInstalledDlcLicenses(const std::filesystem::path& user_root);

}  // namespace fm4_static_recomp
