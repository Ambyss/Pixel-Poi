#pragma once

#include <SD.h>

#define SD_CS_PIN 7

#define SPI_SCK_PIN 4
#define SPI_MISO_PIN 5
#define SPI_MOSI_PIN 6

class SDCardService {
    File _currentFile;

public:
    bool begin();

    bool openFileForWrite(uint8_t imgIndex);
    bool writeChunk(const uint8_t* data, size_t len);
    void closeFile();

    uint8_t* getImgData(uint8_t imgIndex, size_t &size);
};