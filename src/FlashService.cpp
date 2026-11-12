#include "../inc/FlashService.h"

#include <new>

#define IMG_HEAP_HEADROOM 8192

SPIClass spi(FSPI);

bool SDCardService::begin() {
    spi.begin(SPI_SCK_PIN, SPI_MISO_PIN, SPI_MOSI_PIN);
    return SD.begin(SD_CS_PIN, spi);
}

bool SDCardService::openFileForWrite(uint8_t imgIndex) {
    if (_currentFile) {
        _currentFile.close();
    }
    char filename[16];
    snprintf(filename, sizeof(filename), "/%d.bin", imgIndex);
    if (SD.exists(filename)) {
        SD.remove(filename);
    }
    _currentFile = SD.open(filename, FILE_WRITE);
    const bool ok = _currentFile;
    Serial.printf("[F] Open %s for write: %d\n", filename, ok);
    return ok;
}

bool SDCardService::writeChunk(const uint8_t* data, const size_t len) {
    if (!_currentFile) {
        Serial.println("[F] writeChunk: file not opened");
        return false;
    }
    size_t written = _currentFile.write(data, len);
    if (written != len) {
        Serial.printf("[F] writeChunk: short write %u/%u\n", (unsigned)written, (unsigned)len);
        return false;
    }
    _currentFile.flush();
    return true;
}

void SDCardService::closeFile() {
    if (_currentFile) {
        _currentFile.close();
        Serial.println("[F] File closed.");
    }
}

uint8_t* SDCardService::getImgData(uint8_t imgIndex, size_t &size) {
    char filename[16];
    snprintf(filename, sizeof(filename), "/%d.bin", imgIndex);

    File file = SD.open(filename, FILE_READ);
    if (!file) {
        Serial.printf("[F] Failed to open file %s for reading\n", filename);
        return nullptr;
    }

    size = file.size();
    if (size == 0) {
        Serial.println("[F] File is empty");
        file.close();
        return nullptr;
    }

    if (size + IMG_HEAP_HEADROOM > ESP.getMaxAllocHeap()) {
        Serial.printf("[F] Img %s is %u bytes, largest free block is %u - skipped\n",
                      filename, (unsigned)size, (unsigned)ESP.getMaxAllocHeap());
        file.close();
        size = 0;
        return nullptr;
    }

    uint8_t* buffer = new(std::nothrow) uint8_t[size];
    if (!buffer) {
        Serial.printf("[F] Failed to allocate %u bytes for img %s\n", (unsigned)size, filename);
        file.close();
        size = 0;
        return nullptr;
    }

    if (file.read(buffer, size) != (int)size) {
        Serial.println("[F] Failed to read file");
        delete[] buffer;
        file.close();
        size = 0;
        return nullptr;
    }

    file.close();
    return buffer;
}
