// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <unordered_set>

namespace fm4_static_recomp {

struct MusicStream {
  uint32_t object = 0;
  uint32_t playback = 0;
  float volume = 0.0f;
  bool paused = true;
};

class MusicState {
 public:
  void SetStreams(const std::array<MusicStream, 4>& streams) {
    for (size_t slot = 0; slot < streams.size(); ++slot) {
      if (streams_[slot].object != streams[slot].object ||
          (streams[slot].playback && streams_[slot].playback != streams[slot].playback)) {
        ++generations_[slot];
      }
    }
    streams_ = streams;
  }

  void SetRaceSession(bool race) { race_session_ = race; }

  void SetMoviePlaying(uint32_t movie, bool playing) {
    if (playing) movies_.insert(movie);
    else movies_.erase(movie);
  }

  const MusicStream& Stream(size_t slot) const { return streams_[slot]; }
  uint64_t Generation(size_t slot) const { return generations_[slot]; }
  bool Paused(size_t slot) const { return (race_session_ && (slot == 0 || slot == 3)) ||
           !movies_.empty() || !streams_[slot].object || streams_[slot].paused; }
  float Gain(size_t slot) const { return Paused(slot) ? 0.0f : streams_[slot].volume; }

 private:
  bool race_session_ = false;
  std::unordered_set<uint32_t> movies_;
  std::array<MusicStream, 4> streams_{};
  std::array<uint64_t, 4> generations_{};
};

}
