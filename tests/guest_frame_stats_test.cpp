// Copyright (c) 2026, FM4 Static Recomp contributors. BSD-3-Clause.
#include <catch2/catch_test_macros.hpp>

#include <cmath>

#include "ui/guest_frame_stats.h"

using fm4_static_recomp::ui::GuestFrameStats;
using namespace std::chrono_literals;

TEST_CASE("Guest FPS counts game images independently of UI refresh") {
  GuestFrameStats sampler;
  const auto start = GuestFrameStats::Clock::time_point{};
  sampler.Sample(0, start);
  GuestFrameStats::Stats stats;
  // 120 UI redraws per second, with a new guest image on every other redraw.
  for (int redraw = 1; redraw <= 120; ++redraw) {
    stats = sampler.Sample(redraw / 2, start + std::chrono::nanoseconds(
        int64_t(redraw) * 1'000'000'000 / 120));
  }
  REQUIRE(std::abs(stats.fps - 60.0) < 0.01);
  REQUIRE(std::abs(stats.frame_time_ms - 1000.0 / 60.0) < 0.01);
  REQUIRE(stats.frame_count == 60);
}

TEST_CASE("Guest FPS handles stalled output and counter resets") {
  GuestFrameStats sampler;
  const auto start = GuestFrameStats::Clock::time_point{};
  sampler.Sample(100, start);
  REQUIRE(sampler.Sample(130, start + 250ms).fps == 120.0);
  const auto stalled = sampler.Sample(130, start + 500ms);
  REQUIRE(stalled.fps == 0.0);
  REQUIRE(stalled.frame_time_ms == 0.0);
  REQUIRE(sampler.Sample(0, start + 750ms).fps == 0.0);
  REQUIRE(sampler.Sample(15, start + 1000ms).fps == 60.0);
}
