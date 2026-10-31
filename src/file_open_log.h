// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
#pragma once

#include <string>
#include <string_view>

namespace fm4_static_recomp {

// fm4_log_file_opens: logs every guest file the game opens, so the render tests' runner can tell
// which screen is up.
std::string FileOpenLogLine(std::string_view guest_path);

void LogFileOpen(std::string_view guest_path);

}  // namespace fm4_static_recomp
