#include <../inc/main.h>

int period = PERIOD_MAX;

BluetoothService bluetooth;
SDCardService sdCard;
StripService strip_service;

void setupBLECallbacks();

void setup()
{
    Serial.begin(115200);
    sdCard.begin();
    strip_service.begin();
    bluetooth.begin();

    setupBLECallbacks();
}

void loop()
{
    strip_service.execute_loop();
    delay(period);
}

void setupBLECallbacks()
{
    bluetooth.setOnStartFile([](uint8_t imgIndex) -> bool
    {
        return sdCard.openFileForWrite(imgIndex);
    });
    bluetooth.setOnTransmitData([](const uint8_t* data, size_t len) -> bool
    {
        return sdCard.writeChunk(data, len);
    });
    bluetooth.setOnCloseFile([]()
    {
        sdCard.closeFile();
    });
    bluetooth.setOnOpenImg([](const uint8_t imgIndex) -> bool
    {
        strip_service.releaseImg();
        size_t fileSize = 0;
        const uint8_t* data = sdCard.getImgData(imgIndex, fileSize);
        if (!data)
            return false;
        strip_service.applyImg(data, fileSize);
        return true;
    });
    bluetooth.setOnChangeBrightness([](const uint8_t value) -> bool
    {
        strip_service.applyBrightness(value);
        return true;
    });
    bluetooth.setOnChangeSpeed([](const uint8_t value) -> bool
    {
        period = PERIOD_MAX - (PERIOD_MAX - PERIOD_MIN) * value / 100;
        printf("Period changed to %hhu \r\n", period);
        return true;
    });
}
