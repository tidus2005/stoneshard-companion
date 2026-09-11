#pragma once
#include <windows.h>
#include <stdint.h>
#include <stddef.h>

#define BRIDGE_MAGIC 0x53484331u
#define BRIDGE_VERSION 14u
#define BRIDGE_MESSAGE (WM_APP + 0x437)
#define CMD_REFRESH 1
#define CMD_SPEED 2
#define CMD_CENTER 3
#define CMD_PLAYER 4
#define CMD_VISOR 5
#define CMD_RESET 6
#define CMD_DIAGNOSTIC 7

// Single controller, single main-thread consumer. Request/status use separate
// sequence counters, updated last using interlocked release barriers.
typedef struct SharedState {
    uint32_t magic;                 // 0
    uint32_t version;               // 4
    uint32_t pid;                   // 8
    uint32_t ready;                 // 12
    uint64_t process_start;         // 16
    volatile LONG64 heartbeat;     // 24 real GetTickCount64
    volatile LONG request_seq;      // 32
    uint32_t command;               // 36
    double argument;               // 40
    uint64_t deadline;              // 48
    volatile LONG status_seq;       // 56 seqlock
    volatile LONG ack_seq;          // 60
    int32_t error;                  // 64
    uint32_t capabilities;          // 68
    double base_speed;              // 72
    double target_speed;            // 80
    double multiplier;              // 88
    double camera_x;                // 96
    double camera_y;                // 104
    double camera_w;                // 112
    double camera_h;                // 120
    double map_w;                   // 128
    double map_h;                   // 136
    double player_x;                // 144
    double player_y;                // 152
    int32_t camera_mode;            // 160
    int32_t visor_state;            // 164
    uint64_t game_hwnd;             // 168
    uint64_t samples;               // 176
    char detail[512];               // 184
    char diagnostic[3072];          // 696
    int32_t auto_center;             // 3768
    uint32_t scene_ready;            // 3772
    uint64_t scene_generation;       // 3776
    uint64_t window_generation;      // 3784
    double preferred_multiplier;     // 3792
    uint32_t suspend_reasons;        // 3800: 1 background, 2 scene, 4 panel, 8 heartbeat
    uint32_t ui_flags;               // 3804: 1 panel, 2 dialog, 4 confirm, 8 hover, 16 world map
    double hunger,thirst,pain,intoxication; // 3808..3832
    double gui_width,gui_height;     // 3840..3848
    uint32_t vital_valid;            // 3856
    int32_t highlight_state;         // 3860
    int32_t torch_state,water_uses;   // 3864..3868
    uint64_t updated_at;             // 3872 real monotonic engine publication time
    uint64_t request_window_generation; // 3880
    uint64_t request_scene_generation;  // 3888
    uint32_t supply_flags;          // 3896: 1 can act, 2 safe for automation, 4 torch maintenance available
    int32_t torch_count;             // 3900
    double torch_duration;           // 3904; -1 when unavailable
    uint32_t highlight_applied;      // 3912 independent from requested state
    uint32_t walk_state;             // 3916
    int32_t walk_direction;          // 3920: 1..4 N/S/W/E, 5 center, 6..9 NW/NE/SW/SE; 11..14 character-relative
    uint32_t label_hook_ready;       // 3924
    double walk_x,walk_y;            // 3928,3936
    uint64_t label_draw_calls,label_draw_overrides; // 3944,3952
    uint32_t walk_phase;             // 3960: 0 approach/center, 1 exit tile
    uint32_t walk_keys_enabled;      // 3964: unmodified arrow key intent
    char telemetry[57344]; // 3968: bounded UTF-8 JSON, same seqlock as state
    char fodder_selection[2048]; // 61312: request payload, same command seqlock
} SharedState;

_Static_assert(sizeof(SharedState) <= 65536, "mapping bounds");
_Static_assert(offsetof(SharedState,scene_ready)==3772,"scene ABI");
_Static_assert(offsetof(SharedState,preferred_multiplier)==3792,"intent ABI");
_Static_assert(offsetof(SharedState,vital_valid)==3856,"vitals ABI");
_Static_assert(offsetof(SharedState,request_scene_generation)==3888,"generation ABI");
_Static_assert(offsetof(SharedState,supply_flags)==3896,"supplies ABI");
_Static_assert(offsetof(SharedState,torch_count)==3900,"torch count ABI");
_Static_assert(offsetof(SharedState,torch_duration)==3904,"torch duration ABI");
_Static_assert(offsetof(SharedState,highlight_applied)==3912,"highlight applied ABI");
_Static_assert(offsetof(SharedState,walk_x)==3928,"walk target ABI");
_Static_assert(offsetof(SharedState,walk_phase)==3960,"walk phase ABI");
_Static_assert(offsetof(SharedState,walk_keys_enabled)==3964,"walk keys ABI");
_Static_assert(offsetof(SharedState,telemetry)==3968,"telemetry ABI");
