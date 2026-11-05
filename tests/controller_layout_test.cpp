// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
#include <catch2/catch_test_macros.hpp>

#include "input/controller_layout.h"

using namespace fm4_static_recomp::input;

TEST_CASE("Layout 0 is the layout the default keys were laid out for", "[controller_layout]") {
  CHECK(DrivingAction(0, PadControl::kRightTrigger) == "Throttle");
  CHECK(DrivingAction(0, PadControl::kLeftTrigger) == "Brake");
  CHECK(DrivingAction(0, PadControl::kA) == "Handbrake");
  CHECK(DrivingAction(0, PadControl::kB) == "Gear up");
  CHECK(DrivingAction(0, PadControl::kX) == "Gear down");
  CHECK(DrivingAction(0, PadControl::kY) == "Rewind");
  CHECK(DrivingAction(0, PadControl::kLeftShoulder) == "Clutch");
  CHECK(DrivingAction(0, PadControl::kRightShoulder) == "Look straight back");
  CHECK(DrivingAction(0, PadControl::kRStickPress) == "Heat and damage");
  CHECK(DrivingAction(0, PadControl::kDpadUp) == "Telemetry up");
  CHECK(DrivingAction(0, PadControl::kDpadDown) == "Telemetry down");
  CHECK(DrivingAction(0, PadControl::kDpadLeft).empty());
  CHECK(DrivingAction(0, PadControl::kLStickUp).empty());
}

TEST_CASE("Other layouts move the actions between the buttons", "[controller_layout]") {
  CHECK(DrivingAction(7, PadControl::kA) == "Throttle");
  CHECK(DrivingAction(7, PadControl::kRightTrigger) == "Gear up");
  CHECK(DrivingAction(11, PadControl::kRStickUp) == "Throttle");
  CHECK(DrivingAction(11, PadControl::kRStickLeft).empty());
  CHECK(DrivingAction(12, PadControl::kLeftTrigger) == "Throttle");
}

TEST_CASE("Steering, the camera and pause are the same in every layout", "[controller_layout]") {
  for (uint32_t layout = 0; layout < kControllerLayoutCount; ++layout) {
    CHECK(DrivingAction(layout, PadControl::kLStickLeft) == "Steer left");
    CHECK(DrivingAction(layout, PadControl::kLStickRight) == "Steer right");
    CHECK(DrivingAction(layout, PadControl::kBack) == "Camera");
    CHECK(DrivingAction(layout, PadControl::kStart) == "Pause");
  }
}

TEST_CASE("An unknown layout reads as the default one", "[controller_layout]") {
  CHECK(DrivingAction(kControllerLayoutCount, PadControl::kB) == "Gear up");
}

TEST_CASE("The layout the game applied is kept for the tabs", "[controller_layout]") {
  SetControllerLayout(5);
  CHECK(ControllerLayout() == 5);
  SetControllerLayout(0);
  CHECK(ControllerLayout() == 0);
}
