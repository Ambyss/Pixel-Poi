#include <iostream>
#include <../inc/StripService.h>

Adafruit_NeoPixel strip(NUM_PIXELS, LED_PIN, NEO_GRB + NEO_KHZ800);

void StripService::begin() {
    strip.begin();
    applyBrightness(LED_BRIGHTNESS_DEFAULT);
    strip.show();
}

uint32_t StripService::rgb565_to_rgb888(uint16_t color565) {
    uint8_t r5 = (color565 >> 11) & 0x1F;
    uint8_t g6 = (color565 >> 5) & 0x3F;
    uint8_t b5 = color565 & 0x1F;

    uint8_t r8 = (r5 << 3) | (r5 >> 2);
    uint8_t g8 = (g6 << 2) | (g6 >> 4);
    uint8_t b8 = (b5 << 3) | (b5 >> 2);

    return strip.Color(r8, g8, b8);
}

void StripService::displaySingleColorColumn(int col_to_display) {

    // Safety check
    if (col_to_display < 0 || col_to_display >= bitmap_width) {
        strip.clear();
        strip.show();
        return;
    }

    strip.clear();

    for (int y = 0; y < NUM_PIXELS; y++) {

        int pixelIndex = (col_to_display * NUM_PIXELS) + y;

        int byteIndex = pixelIndex * 2;

        const uint8_t lsb = bitmap_data[byteIndex];
        const uint8_t msb = bitmap_data[byteIndex + 1];

        uint16_t color565 = (uint16_t)msb << 8 | lsb;

        const uint32_t finalColor = rgb565_to_rgb888(color565);

        strip.setPixelColor(y, finalColor);
    }

    strip.show();
}

void StripService::applyImg(const uint8_t *data, size_t fileSize) {
    if (!data || fileSize == 0) {
        Serial.println("[S] applyImg: no data");
        return;
    }
    releaseImg();
    bitmap_data = data;
    bitmap_width = fileSize / (NUM_PIXELS * 2);
    curWidthPixel = 0;
    Serial.printf("[S] Open img %u bytes, %u columns\n", (unsigned)fileSize, bitmap_width);
}

void StripService::releaseImg() {
    delete[] bitmap_data;
    bitmap_data = nullptr;
    bitmap_width = 0;
    curWidthPixel = 0;
    strip.clear();
    strip.show();
}

void StripService::applyColor(int r, int g, int b) {
    for(int i=0; i < NUM_PIXELS; i++)
    {
        strip.setPixelColor(i, Adafruit_NeoPixel::Color(r, g, b));
    }
    strip.show();
}

void StripService::applyBrightness(int brightness) {
    strip.setBrightness(brightness);
    applyColor(255, 0, 0);
}

void StripService::execute_loop() {
    if (!bitmap_data)
        return;
    displaySingleColorColumn(curWidthPixel);
    increasePixelIndex();
}

void StripService::increasePixelIndex() {
    if (++curWidthPixel >= bitmap_width) {
        curWidthPixel = 0;
    }
}
