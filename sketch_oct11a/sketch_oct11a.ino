#include <Adafruit_NeoPixel.h>
#include "color_bitmap.h" // Includes myColorBitmap[], BITMAP_WIDTH, and BITMAP_HEIGHT

// --- CONFIGURATION (from previous example) ---
#define LED_PIN       2
#define NUM_PIXELS   36
#define LED_BRIGHTNESS 2
Adafruit_NeoPixel strip(NUM_PIXELS, LED_PIN, NEO_GRB + NEO_KHZ800);
const uint32_t COLOR_OFF = Adafruit_NeoPixel::Color(0, 0, 0); // Black for clearing

// --- RGB565 TO RGB888 CONVERSION (REQUIRED) ---
uint32_t rgb565_to_rgb888(uint16_t color565) {
  // Extract 5-bit Red (R), 6-bit Green (G), 5-bit Blue (B)
  uint8_t r5 = (color565 >> 11) & 0x1F;
  uint8_t g6 = (color565 >> 5) & 0x3F;
  uint8_t b5 = color565 & 0x1F;
  
  // Convert to 8-bit (r5 -> r8, g6 -> g8, b5 -> b8)
  uint8_t r8 = (r5 << 3) | (r5 >> 2);
  uint8_t g8 = (g6 << 2) | (g6 >> 4);
  uint8_t b8 = (b5 << 3) | (b5 >> 2);
  
  return strip.Color(r8, g8, b8);
}

// ----------------------------------------------------------------------
// FUNCTION TO DISPLAY A SINGLE VERTICAL COLUMN OF THE COLOR BITMAP
// ----------------------------------------------------------------------

void displaySingleColorColumn(int col_to_display) {
  
  // Safety check
  if (col_to_display < 0 || col_to_display >= BITMAP_WIDTH) {
    strip.clear();
    strip.show();
    return;
  }
  
  // Clear the entire strip (or just the portion you're using, like the first 8 pixels)
  strip.clear(); 
  
  // Iterate through the rows (Y-axis) of the column
  for (int y = 0; y < BITMAP_HEIGHT; y++) {
    
    // 1. Calculate the pixel index in the linear bitmap data
    // Index = (Column_X * Height) + Row_Y
    int pixelIndex = (col_to_display * BITMAP_HEIGHT) + y;
    
    // 2. Calculate the byte index for the start of this pixel's 2-byte data
    // Each pixel takes 2 bytes (RGB565)
    int byteIndex = pixelIndex * 2; 

    // 3. Read the two bytes for the 16-bit color value from PROGMEM
    // Assuming Little-Endian order (LSB first, then MSB)
    uint8_t lsb = pgm_read_byte(&myColorBitmap[byteIndex]);
    uint8_t msb = pgm_read_byte(&myColorBitmap[byteIndex + 1]);
    
    // 4. Reconstruct the 16-bit RGB565 color
    uint16_t color565 = (uint16_t)msb << 8 | lsb;
    
    // 5. Convert 16-bit RGB565 to 24-bit RGB888
    uint32_t finalColor = rgb565_to_rgb888(color565);
    
    // 6. Set the color of the corresponding LED on the strip
    // We map the 8 pixels of the column to the first 8 LEDs of the strip (index 0 to 7)
    strip.setPixelColor(y, finalColor); 
  }

  // Display the changes
  strip.show();
}

// ----------------------------------------------------------------------
// ARDUINO CORE FUNCTIONS (SETUP/LOOP)
// ----------------------------------------------------------------------

void setup() {
  strip.begin();
  strip.setBrightness(LED_BRIGHTNESS);
  strip.show();
}

void loop() {
  // Cycle through columns 0 to 7
  for (int col = 0; col < BITMAP_WIDTH; col++) {
    displaySingleColorColumn(col);
    delay(50); // Display each column for half a second
  }
}