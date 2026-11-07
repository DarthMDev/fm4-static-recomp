// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
#pragma once

#include "music_state.h"

namespace fm4_static_recomp {

void InitializeCustomMusic();
bool CustomMusicEnabled();
bool ReplacesMusicSlot(size_t slot);
bool CustomMusicPlaying(size_t slot, uint32_t stream, bool& playing);
void ObserveMusicStreams(uint32_t manager, const std::array<MusicStream, 4>& streams);
void SetCustomMusicMoviePlaying(uint32_t movie, bool playing);
void SetCustomMusicRaceSession(bool race);
void UpdateCustomMusic();
void ShutdownCustomMusic();

}
