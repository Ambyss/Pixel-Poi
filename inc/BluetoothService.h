#pragma once

#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>
#include <functional>
#include <utility>

extern BLEServer* pServer;
extern BLECharacteristic* pTxCharacteristic;
extern BLECharacteristic* pRxCharacteristic;

extern bool deviceConnected;

#define SERVICE_UUID "5E400001-B5A3-F393-E0A9-E50E24DCCA9E"
#define CHARACTERISTIC_UUID_RX "5E400002-B5A3-F393-E0A9-E50E24DCCA9E"
#define CHARACTERISTIC_UUID_TX "5E400003-B5A3-F393-E0A9-E50E24DCCA9E"

#define OPEN_FILE_CMD 0x01
#define TRANSMIT_DATA_CMD 0x02
#define CLOSE_FILE_CMD 0x03
#define OPEN_IMG_CMD 0x04
#define CHANGE_BRIGHTNESS_CMD 0x08
#define CHANGE_SPEED_CMD 0x09

#define ACK_OK 0xA0
#define ACK_FAIL 0xA1

#define CMD_ACK_FLAG 0x8000

class BluetoothService
{
public:
    using CommandCallback = std::function<void()>;
    using TransmitDataCallback = std::function<bool(const uint8_t* data, size_t len)>;
    using ImgCallback = std::function<bool(uint8_t imgIndex)>;
    using ValueCallback = std::function<void(uint8_t value)>;

private:
    CommandCallback _onCloseFile;
    ValueCallback _onBrightnessChange;
    ValueCallback _onSpeedChange;

    TransmitDataCallback _onTransmitData;

    ImgCallback _onStartFile;
    ImgCallback _onOpenImg;

    void sendAck(uint8_t status, uint16_t seq) const;

public:
    void begin();

    void setOnTransmitData(TransmitDataCallback cb) { _onTransmitData = std::move(cb); }
    void setOnStartFile(ImgCallback cb) { _onStartFile = std::move(cb); }
    void setOnCloseFile(CommandCallback cb) { _onCloseFile = std::move(cb); }
    void setOnOpenImg(ImgCallback cb) { _onOpenImg = std::move(cb); }
    void setOnChangeBrightness(ValueCallback cb) { _onBrightnessChange = std::move(cb); }
    void setOnChangeSpeed(ValueCallback cb) { _onSpeedChange = std::move(cb); }

    void receiveData(const uint8_t* data, size_t len, uint8_t dataType) const;

    void callOnConnect() const;

    void callOnDisconnect() const;
};

class MyCallbacks : public BLEServerCallbacks
{
private:
    BluetoothService* _service;

public:
    explicit MyCallbacks(BluetoothService* s) : _service(s)
    {
    }

    void onConnect(BLEServer* pServer) override;
    void onDisconnect(BLEServer* pServer) override;
};

class MyRxCallbacks : public BLECharacteristicCallbacks
{
private:
    BluetoothService* _service;

public:
    explicit MyRxCallbacks(BluetoothService* s) : _service(s)
    {
    }

    void onWrite(BLECharacteristic* pCharacteristic) override;
};
