// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
#include <catch2/catch_test_macros.hpp>

#include <array>
#include <bit>
#include <cstring>

#include "audio_gain.h"

namespace {

void Word(uint8_t* p, uint32_t value) {
  value = std::byteswap(value);
  std::memcpy(p, &value, 4);
}

uint32_t Word(const uint8_t* p) {
  uint32_t value;
  std::memcpy(&value, p, 4);
  return std::byteswap(value);
}

void Float(uint8_t* p, float value) { Word(p, std::bit_cast<uint32_t>(value)); }
float Float(const uint8_t* p) { return std::bit_cast<float>(Word(p)); }

struct Fixture {
  std::array<uint8_t, 0x320> state{};
  std::array<uint8_t, 64> input{};
  std::array<uint8_t, 64> output{};

  Fixture() {
    Word(state.data() + 0x1a0, 1000);
    for (uint32_t c = 0; c < 2; ++c) {
      Word(state.data() + 0x1d0 + c * 4, 10);
      Word(state.data() + 0x290 + c * 4, 100);
      Float(state.data() + 0x210 + c * 4, c ? -0.5f : 0.25f);
      Float(state.data() + 0x250 + c * 4, c ? 0.125f : 0.25f);
    }
    for (uint32_t i = 0; i < 16; ++i) Float(input.data() + i * 4, 2.0f);
    output.fill(0xa5);
  }

  uint32_t Run(uint32_t frames) {
    return fm4_static_recomp::ProcessStableAudioGain(state.data(), input.data(), output.data(),
                                                    frames, 2);
  }
};

}

TEST_CASE("Audio gain preserves interleaved samples and ramp state", "[audio_gain]") {
  Fixture f;
  REQUIRE(f.Run(3) == 3);
  const std::array<float, 6> expected{0.5f, -1.0f, 1.0f, -0.75f, 1.5f, -0.5f};
  for (size_t i = 0; i < expected.size(); ++i) CHECK(Float(f.output.data() + i * 4) == expected[i]);
  CHECK(Float(f.state.data() + 0x210) == 1.0f);
  CHECK(Float(f.state.data() + 0x214) == -0.125f);
  CHECK(Word(f.state.data() + 0x1d0) == 13);
  CHECK(Word(f.state.data() + 0x1d4) == 13);
  CHECK(Word(f.output.data() + 24) == 0xa5a5a5a5);
}

TEST_CASE("Audio gain stops before coefficient updates or phase wrapping", "[audio_gain]") {
  Fixture f;
  SECTION("Coefficient boundary") { Word(f.state.data() + 0x294, 12); }
  SECTION("Phase boundary") { Word(f.state.data() + 0x1a0, 13); }
  REQUIRE(f.Run(8) == 2);
  CHECK(Word(f.state.data() + 0x1d0) == 12);
  CHECK(Word(f.output.data() + 16) == 0xa5a5a5a5);
  auto state = f.state;
  auto output = f.output;
  CHECK(f.Run(6) == 0);
  CHECK(f.state == state);
  CHECK(f.output == output);
}

TEST_CASE("Audio gain supports in-place mixing and zero work", "[audio_gain]") {
  Fixture f;
  auto original = f.state;
  CHECK(f.Run(0) == 0);
  CHECK(f.state == original);
  REQUIRE(fm4_static_recomp::ProcessStableAudioGain(f.state.data(), f.input.data(), f.input.data(),
                                                  2, 2) == 2);
  CHECK(Float(f.input.data()) == 0.5f);
  CHECK(Float(f.input.data() + 4) == -1.0f);
  CHECK(Float(f.input.data() + 8) == 1.0f);
  CHECK(Float(f.input.data() + 12) == -0.75f);
}

TEST_CASE("Audio gain rounds each ramp step instead of combining increments", "[audio_gain]") {
  Fixture f;
  Float(f.state.data() + 0x210, 1.0f);
  Float(f.state.data() + 0x250, 0x1p-24f);
  REQUIRE(f.Run(8) == 8);
  CHECK(Word(f.state.data() + 0x210) == std::bit_cast<uint32_t>(1.0f));
  for (size_t frame = 0; frame < 8; ++frame) CHECK(Float(f.output.data() + frame * 8) == 2.0f);
}

TEST_CASE("Audio gain preserves negative counters and waits at an overdue update", "[audio_gain]") {
  Fixture f;
  Word(f.state.data() + 0x1d0, uint32_t(-1));
  Word(f.state.data() + 0x290, 0);
  REQUIRE(f.Run(8) == 1);
  CHECK(Word(f.state.data() + 0x1d0) == 0);
  CHECK(Word(f.state.data() + 0x1d4) == 11);
  const auto state = f.state;
  const auto output = f.output;
  CHECK(f.Run(8) == 0);
  CHECK(f.state == state);
  CHECK(f.output == output);
}

TEST_CASE("Audio gain uses the earliest boundary across all sixteen channel slots", "[audio_gain]") {
  Fixture f;
  for (uint32_t channel = 0; channel < 16; ++channel) {
    Word(f.state.data() + 0x1d0 + channel * 4, 10);
    Word(f.state.data() + 0x290 + channel * 4, channel == 15 ? 11 : 100);
    Float(f.state.data() + 0x210 + channel * 4, 0.5f);
    Float(f.state.data() + 0x250 + channel * 4, 0.125f);
  }
  REQUIRE(fm4_static_recomp::ProcessStableAudioGain(f.state.data(), f.input.data(), f.output.data(),
                                                  4, 16) == 1);
  for (uint32_t channel = 0; channel < 16; ++channel) {
    CHECK(Float(f.output.data() + channel * 4) == 1.0f);
    CHECK(Float(f.state.data() + 0x210 + channel * 4) == 0.625f);
    CHECK(Word(f.state.data() + 0x1d0 + channel * 4) == 11);
  }
}
