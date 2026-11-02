// Copyright (c) 2026, FM4 Static Recomp contributors. BSD-3-Clause.
#pragma once

#include <chrono>
#include <cstdint>

namespace fm4_static_recomp::ui {

// Sample guest output notifications, never the frequency of overlay redraws.
class GuestFrameStats {
 public:
  using Clock = std::chrono::steady_clock;
  struct Stats {
    double frame_time_ms = 0.0;
    double fps = 0.0;
    uint64_t frame_count = 0;
  };

  Stats Sample(uint64_t frames, Clock::time_point now) {
    if (!initialized_ || frames < last_frames_) {
      initialized_ = true;
      last_time_ = now;
      last_frames_ = frames;
      stats_ = {};
    }
    const double elapsed = std::chrono::duration<double>(now - last_time_).count();
    if (elapsed >= 0.25) {
      stats_.fps = static_cast<double>(frames - last_frames_) / elapsed;
      stats_.frame_time_ms = stats_.fps > 0.0 ? 1000.0 / stats_.fps : 0.0;
      last_time_ = now;
      last_frames_ = frames;
    }
    stats_.frame_count = frames;
    return stats_;
  }

 private:
  bool initialized_ = false;
  Clock::time_point last_time_{};
  uint64_t last_frames_ = 0;
  Stats stats_{};
};

}  // namespace fm4_static_recomp::ui
