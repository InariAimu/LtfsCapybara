/**
 * Copyright (c) 2020 Raspberry Pi (Trading) Ltd.
 *
 * SPDX-License-Identifier: BSD-3-Clause
 */

#include "pico/stdlib.h"
#include <stdbool.h>
#include <stdint.h>

/*
   7-segment layout (index mapping):
     0 = A
     1 = B
     2 = C
     3 = D
     4 = E
     5 = F
     6 = G
     7 = DP (decimal point)

   The pin mapping and active level for each segment is configurable
   via the `segments` array below.
*/

typedef struct {
    uint gpio;         // GPIO pin number
    bool active_high;  // true = segment lights when gpio is 1
} Segment;

// Default mapping: GPIO 0..7, active LOW. Edit these entries to match your wiring.
static const Segment segments[8] = {
    {0, false}, // A
    {1, false}, // B
    {2, false}, // C
    {3, false}, // D
    {4, false}, // E
    {5, false}, // F
    {6, false}, // G
    {7, false}  // DP
};

// Digit bit patterns (bit0 = A, bit1 = B, ... bit6 = G). Common patterns used by
// many 7-seg displays. DP is bit 7 when used separately.
static const uint8_t digit_patterns[10] = {
    0x3F, // 0: 0b00111111 (A B C D E F)
    0x06, // 1: 0b00000110 (B C)
    0x5B, // 2: 0b01011011
    0x4F, // 3: 0b01001111
    0x66, // 4: 0b01100110
    0x6D, // 5: 0b01101101
    0x7D, // 6: 0b01111101
    0x07, // 7: 0b00000111
    0x7F, // 8: 0b01111111
    0x6F  // 9: 0b01101111
};

// Initialize all configured segment GPIOs as outputs and turn them off.
static void init_segments(const Segment segs[8]) {
    for (int i = 0; i < 8; ++i) {
        gpio_init(segs[i].gpio);
        gpio_set_dir(segs[i].gpio, GPIO_OUT);
        // set to the inactive level to start (off)
        int inactive = segs[i].active_high ? 0 : 1;
        gpio_put(segs[i].gpio, inactive);
    }
}

// Set a single segment on or off honoring the configured active level.
static inline void set_segment(const Segment *s, bool on) {
    int val = on ? (s->active_high ? 1 : 0) : (s->active_high ? 0 : 1);
    gpio_put(s->gpio, val);
}

// Display a single decimal digit (0-9). If `dot` is true, the DP segment is lit.
// This function uses the `segments` configuration above.
void display_digit(int digit, bool dot) {
    if (digit < 0 || digit > 9) return;
    uint8_t pat = digit_patterns[digit];
    for (int s = 0; s < 7; ++s) {
        bool on = (pat >> s) & 1;
        set_segment(&segments[s], on);
    }
    // DP is segment index 7
    set_segment(&segments[7], dot);
}

int main() {
    stdio_init_all();
    init_segments(segments);

    // Demo: cycle digits 0..9 showing the decimal point on every other digit.
    int d = 0;
    while (true) {
        display_digit(d, (d & 1) == 0);
        sleep_ms(500);
        d = (d + 1) % 10;
    }
}
