// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
#include <catch2/catch_test_macros.hpp>
#include <catch2/catch_approx.hpp>

#include "music_state.h"

TEST_CASE("Native music pause gates title and countdown without changing tracks", "[music]") {
  fm4_static_recomp::MusicState state;
  std::array<fm4_static_recomp::MusicStream, 4> streams{};
  streams[0] = {10, 20, 0.4f, true};
  state.SetStreams(streams);
  const auto generation = state.Generation(0);
  CHECK(state.Gain(0) == 0.0f);
  CHECK(state.Paused(0));
  streams[0].paused = false;
  state.SetStreams(streams);
  CHECK(state.Gain(0) == Catch::Approx(0.4f));
  CHECK(state.Generation(0) == generation);
  streams[0].volume = 0.0f;
  state.SetStreams(streams);
  CHECK_FALSE(state.Paused(0));
  CHECK(state.Generation(0) == generation);
}

TEST_CASE("New streams and native playback restarts select new generations", "[music]") {
  fm4_static_recomp::MusicState state;
  std::array<fm4_static_recomp::MusicStream, 4> streams{};
  streams[1] = {10, 20, 0.7f, false};
  state.SetStreams(streams);
  const auto first = state.Generation(1);
  streams[1].volume = 0.35f;
  state.SetStreams(streams);
  CHECK(state.Generation(1) == first);
  CHECK(state.Gain(1) == Catch::Approx(0.35f));
  streams[1].playback = 21;
  state.SetStreams(streams);
  CHECK(state.Generation(1) == first + 1);
  streams[1] = {};
  state.SetStreams(streams);
  CHECK(state.Paused(1));
  CHECK(state.Gain(1) == 0.0f);
  streams[1] = {10, 22, 0.7f, true};
  state.SetStreams(streams);
  CHECK(state.Generation(1) == first + 3);
}

TEST_CASE("Native overlapping streams retain separate fade envelopes", "[music]") {
  fm4_static_recomp::MusicState state;
  state.SetStreams({{{10, 20, 0.4f, false}, {11, 21, 0.2f, false},
                     {12, 22, 0.7f, true}, {13, 23, 0.1f, false}}});
  CHECK(state.Gain(0) == Catch::Approx(0.4f));
  CHECK(state.Gain(1) == Catch::Approx(0.2f));
  CHECK(state.Gain(2) == 0.0f);
  CHECK(state.Gain(3) == Catch::Approx(0.1f));
}

TEST_CASE("Native movie lifetimes suppress music through overlapping playback", "[music]") {
  fm4_static_recomp::MusicState state;
  state.SetStreams({{{10, 20, 0.4f, false}, {}, {}, {}}});
  const auto generation = state.Generation(0);
  state.SetMoviePlaying(100, true);
  CHECK(state.Paused(0));
  CHECK(state.Gain(0) == 0.0f);
  state.SetMoviePlaying(100, true);
  state.SetMoviePlaying(101, true);
  state.SetMoviePlaying(100, false);
  CHECK(state.Paused(0));
  state.SetMoviePlaying(101, false);
  CHECK_FALSE(state.Paused(0));
  CHECK(state.Gain(0) == Catch::Approx(0.4f));
  CHECK(state.Generation(0) == generation);
}

TEST_CASE("Race loading silences menu streams before countdown playback", "[music]") {
  fm4_static_recomp::MusicState state;
  state.SetStreams({{{10, 20, 0.4f, false}, {11, 21, 0.7f, true}, {}, {12, 22, 0.2f, false}}});
  state.SetRaceSession(true);
  CHECK(state.Paused(0));
  CHECK(state.Paused(3));
  CHECK(state.Gain(0) == 0.0f);
  CHECK(state.Paused(1));
  state.SetStreams({{{}, {11, 21, 0.7f, false}, {}, {}}});
  CHECK_FALSE(state.Paused(1));
  state.SetRaceSession(false);
  state.SetStreams({{{13, 23, 0.4f, false}, {}, {}, {}}});
  CHECK_FALSE(state.Paused(0));
}
