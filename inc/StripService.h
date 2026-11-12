#pragma once

#include <Adafruit_NeoPixel.h>

#define LED_PIN 0
#define NUM_PIXELS 36 // Height of img
#define LED_BRIGHTNESS_DEFAULT 1

extern Adafruit_NeoPixel strip;

class StripService {
    const uint8_t* bitmap_data = nullptr;
    uint16_t bitmap_width = 0;
    uint16_t curWidthPixel = 0;
    unsigned long timer = 0;
    const uint32_t COLOR_OFF = Adafruit_NeoPixel::Color(0, 0, 0);

    uint32_t rgb565_to_rgb888(uint16_t color565);
    void displaySingleColorColumn(int col_to_display);
    void increasePixelIndex();

public:
    void applyImg(const uint8_t* data, size_t fileSize);
    void releaseImg();
    void applyColor(int r, int g, int b);
    void applyBrightness(int brightness);
    void begin();
    void execute_loop();
};