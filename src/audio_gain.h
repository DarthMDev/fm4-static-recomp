// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
#pragma once

#include <cstdint>

namespace fm4_static_recomp {

uint32_t ProcessStableAudioGain(uint8_t* state, const uint8_t* input, uint8_t* output,
                                uint32_t frames, uint32_t channels);

}
