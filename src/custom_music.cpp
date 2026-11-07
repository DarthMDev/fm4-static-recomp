// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
#include "custom_music.h"

#include <algorithm>
#include <atomic>
#include <filesystem>
#include <memory>
#include <mutex>
#include <random>
#include <string>
#include <vector>

#include <rex/audio/host_music.h>
#include <rex/cvar.h>
#include <rex/filesystem.h>
#include <rex/logging.h>

REXCVAR_DEFINE_STRING(fm4_menu_music_folder, "", "Forza Motorsport 4",
                      "Shuffled menu MP3/WAV folder; empty uses the original soundtrack (restart required)");
REXCVAR_DEFINE_STRING(fm4_race_music_folder, "", "Forza Motorsport 4",
                      "Shuffled race MP3/WAV folder; empty uses the original soundtrack (restart required)");

namespace fm4_static_recomp {
namespace {

struct Playlist {
  std::vector<std::filesystem::path> tracks;
  size_t next = 0;
  std::filesystem::path last;
};

struct Voice {
  std::unique_ptr<rex::audio::HostMusicPlayer> player;
  std::filesystem::path file;
  uint32_t stream = 0;
  uint64_t generation = 0;
  float gain = -1.0f;
  bool paused = true;
  bool started = false;
};

std::mutex g_music_mutex;
std::atomic<bool> g_music_enabled{false};
std::array<std::atomic<bool>, 2> g_replacing{};
uint32_t g_manager = 0;
MusicState g_state;
std::array<Playlist, 2> g_playlists;
std::array<Voice, 4> g_voices;
std::mt19937 g_random;

size_t PlaylistForSlot(size_t slot) { return slot == 0 || slot == 3 ? 0 : 1; }

void StartVoice(Voice& voice, size_t domain) {
  auto& playlist = g_playlists[domain];
  if (playlist.next >= playlist.tracks.size()) {
    std::shuffle(playlist.tracks.begin(), playlist.tracks.end(), g_random);
    if (playlist.tracks.size() > 1 && playlist.tracks.front() == playlist.last) {
      std::swap(playlist.tracks[0], playlist.tracks[1]);
    }
    playlist.next = 0;
  }
  voice.file = playlist.tracks[playlist.next++];
  playlist.last = voice.file;
  if (!voice.player) voice.player = std::make_unique<rex::audio::HostMusicPlayer>();
  voice.player->SetVolume(0.0f);
  voice.player->PlayFile(voice.file, true, 2.0f, 0.5f);
  voice.gain = -1.0f;
  voice.paused = true;
  voice.started = false;
  REXLOG_INFO("Custom {} music selected: {}", domain == 0 ? "menu" : "race",
              rex::path_to_utf8(voice.file.stem()));
}

}

void InitializeCustomMusic() {
  const std::lock_guard lock(g_music_mutex);
  g_random.seed(std::random_device{}());
  const std::array<std::string, 2> folders{REXCVAR_GET(fm4_menu_music_folder),
                                          REXCVAR_GET(fm4_race_music_folder)};
  for (size_t i = 0; i < folders.size(); ++i) {
    if (folders[i].empty()) continue;
    auto& playlist = g_playlists[i];
    playlist.tracks = rex::audio::HostMusicPlayer::FindTracks(rex::to_path(folders[i]));
    if (playlist.tracks.empty()) {
      REXLOG_WARN("Custom {} music: no supported tracks in {}; using the original soundtrack",
                  i == 0 ? "menu" : "race", folders[i]);
      continue;
    }
    playlist.next = playlist.tracks.size();
    REXLOG_INFO("Custom {} music: {} tracks from {}", i == 0 ? "menu" : "race",
                playlist.tracks.size(), folders[i]);
    g_replacing[i].store(true, std::memory_order_release);
    g_music_enabled.store(true, std::memory_order_release);
  }
}

bool CustomMusicEnabled() { return g_music_enabled.load(std::memory_order_acquire); }
bool ReplacesMusicSlot(size_t slot) {
  return g_replacing[PlaylistForSlot(slot)].load(std::memory_order_acquire);
}

void ObserveMusicStreams(uint32_t manager, const std::array<MusicStream, 4>& streams) {
  const std::lock_guard lock(g_music_mutex);
  if (g_manager != manager) {
    g_manager = manager;
    g_state.SetStreams({});
    for (auto& voice : g_voices) voice = {};
  }
  g_state.SetStreams(streams);
}

bool CustomMusicPlaying(size_t slot, uint32_t stream, bool& playing) {
  if (!ReplacesMusicSlot(slot)) return false;
  const std::lock_guard lock(g_music_mutex);
  const auto& voice = g_voices[slot];
  if (!voice.player || voice.stream != stream) return false;
  if (!voice.started) {
    playing = true;
  } else {
    const auto status = voice.player->GetStatus();
    playing = status.playing || status.paused;
  }
  return true;
}

void SetCustomMusicMoviePlaying(uint32_t movie, bool playing) {
  if (!CustomMusicEnabled()) return;
  {
    const std::lock_guard lock(g_music_mutex);
    g_state.SetMoviePlaying(movie, playing);
  }
  REXLOG_INFO("Custom music: native movie {:08X} {}", movie, playing ? "started" : "stopped");
  UpdateCustomMusic();
}

void SetCustomMusicRaceSession(bool race) {
  const std::lock_guard lock(g_music_mutex);
  g_state.SetRaceSession(race);
}

void UpdateCustomMusic() {
  if (!CustomMusicEnabled()) return;
  const std::lock_guard lock(g_music_mutex);
  for (size_t slot = 0; slot < g_voices.size(); ++slot) {
    auto& voice = g_voices[slot];
    const auto& stream = g_state.Stream(slot);
    const size_t domain = PlaylistForSlot(slot);
    if (!ReplacesMusicSlot(slot) || !stream.object) {
      voice = {};
      continue;
    }
    if (voice.stream != stream.object || voice.generation != g_state.Generation(slot)) {
      voice = {};
      voice.stream = stream.object;
      voice.generation = g_state.Generation(slot);
    }
    if (!voice.player && stream.playback) StartVoice(voice, domain);
    if (!voice.player) continue;
    auto status = voice.player->GetStatus();
    voice.started |= status.ready;
    if (!voice.started && !status.playing && !status.paused) {
      auto& playlist = g_playlists[domain];
      std::erase(playlist.tracks, voice.file);
      playlist.next = playlist.tracks.size();
      if (playlist.tracks.empty()) {
        g_replacing[domain].store(false, std::memory_order_release);
        REXLOG_WARN("Custom {} music: no decodable tracks; using the original soundtrack",
                    domain == 0 ? "menu" : "race");
        voice = {};
        continue;
      }
      StartVoice(voice, domain);
      continue;
    }
    const float gain = g_state.Gain(slot);
    const bool paused = g_state.Paused(slot);
    if (gain != voice.gain) {
      voice.player->SetVolume(gain);
      voice.gain = gain;
    }
    if (paused != voice.paused) {
      voice.player->SetPaused(paused);
      voice.paused = paused;
      REXLOG_INFO("Custom {} music {} at volume {:.3f}: {}", domain == 0 ? "menu" : "race",
                  paused ? "paused" : "started", gain, rex::path_to_utf8(voice.file.stem()));
    }
  }
}

void ShutdownCustomMusic() {
  g_music_enabled.store(false, std::memory_order_release);
  for (auto& replacing : g_replacing) replacing.store(false, std::memory_order_release);
  const std::lock_guard lock(g_music_mutex);
  for (auto& voice : g_voices) voice = {};
}

}
