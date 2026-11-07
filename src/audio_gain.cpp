// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
#include "audio_gain.h"

#include <algorithm>
#include <array>
#include <bit>
#include <cstring>

namespace fm4_static_recomp {
namespace {

uint32_t LoadWord(const uint8_t* p) {
  uint32_t value;
  std::memcpy(&value, p, sizeof(value));
  return std::byteswap(value);
}

void StoreWord(uint8_t* p, uint32_t value) {
  value = std::byteswap(value);
  std::memcpy(p, &value, sizeof(value));
}

float LoadFloat(const uint8_t* p) { return std::bit_cast<float>(LoadWord(p)); }
void StoreFloat(uint8_t* p, float value) { StoreWord(p, std::bit_cast<uint32_t>(value)); }

}

uint32_t ProcessStableAudioGain(uint8_t* state, const uint8_t* input, uint8_t* output,
                                uint32_t frames, uint32_t channels) {
  const int64_t period = int32_t(LoadWord(state + 0x1a0));
  std::array<int32_t, 16> counts;
  std::array<float, 16> gains;
  std::array<float, 16> increments;
  for (uint32_t channel = 0; channel < channels; ++channel) {
    const uint32_t offset = channel * 4;
    counts[channel] = int32_t(LoadWord(state + 0x1d0 + offset));
    const int64_t threshold = int32_t(LoadWord(state + 0x290 + offset));
    // The guest wraps after incrementing; leave that frame to its state machine.
    const int64_t available = std::min(threshold - counts[channel], period - counts[channel] - 1);
    if (available <= 0) return 0;
    frames = uint32_t(std::min<int64_t>(frames, available));
    gains[channel] = LoadFloat(state + 0x210 + offset);
    increments[channel] = LoadFloat(state + 0x250 + offset);
  }
  if (!frames) return 0;

  for (uint32_t frame = 0; frame < frames; ++frame) {
    for (uint32_t channel = 0; channel < channels; ++channel) {
      const uint32_t offset = (frame * channels + channel) * 4;
      // Match the recompiler's double arithmetic followed by single rounding.
      StoreFloat(output + offset, float(double(LoadFloat(input + offset)) * double(gains[channel])));
      gains[channel] = float(double(increments[channel]) + double(gains[channel]));
    }
  }
  for (uint32_t channel = 0; channel < channels; ++channel) {
    StoreFloat(state + 0x210 + channel * 4, gains[channel]);
    StoreWord(state + 0x1d0 + channel * 4, uint32_t(counts[channel]) + frames);
  }
  return frames;
}

}
