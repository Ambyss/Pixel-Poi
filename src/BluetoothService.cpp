#include "../inc/BluetoothService.h"
#include <Arduino.h>

BLEServer* pServer = nullptr;
BLECharacteristic* pTxCharacteristic = nullptr;
BLECharacteristic* pRxCharacteristic = nullptr;

bool deviceConnected = false;

static uint16_t expectedSeq = 0;

void BluetoothService::begin()
{
    Serial.println("[B] Starting BLE server...");
    BLEDevice::init("Pipirka-1"); // TODO: Place unique name for each device

    pServer = BLEDevice::createServer();
    pServer->setCallbacks(new MyCallbacks(this));

    BLEService* pService = pServer->createService(SERVICE_UUID);

    pTxCharacteristic = pService->createCharacteristic(
        CHARACTERISTIC_UUID_TX,
        BLECharacteristic::PROPERTY_NOTIFY
    );
    pTxCharacteristic->addDescriptor(new BLE2902());

    pRxCharacteristic = pService->createCharacteristic(
        CHARACTERISTIC_UUID_RX,
        BLECharacteristic::PROPERTY_WRITE
    );
    pRxCharacteristic->setCallbacks(new MyRxCallbacks(this));

    pService->start();
    // Android device requests MTU
    BLEDevice::setMTU(250);
    pServer->getAdvertising()->start();
    Serial.println("[B] BLE server started");
}

void BluetoothService::receiveData(const uint8_t* data, size_t len, uint8_t dataType) const
{
    switch (dataType)
    {
    case OPEN_FILE_CMD:
        {
            const uint8_t imgIndex = data[1];
            expectedSeq = 0;
            bool ok = false;
            if (_onStartFile)
                ok = _onStartFile(imgIndex);
            sendAck(ok ? ACK_OK : ACK_FAIL, CMD_ACK_FLAG | OPEN_FILE_CMD);
            Serial.printf("[B] START id=%hhu ok=%d\n", imgIndex, ok);
        }
        break;

    case TRANSMIT_DATA_CMD:
        {
            if (len < 3)
            {
                sendAck(ACK_FAIL, 0xFFFF);
                return;
            }
            const uint16_t seq = (uint16_t)data[1] | ((uint16_t)data[2] << 8);

            if (seq != expectedSeq)
            {
                sendAck(ACK_FAIL, seq);
                return;
            }

            bool ok = false;
            if (_onTransmitData)
                ok = _onTransmitData(data + 3, len - 3);

            if (ok)
            {
                sendAck(ACK_OK, seq);
                expectedSeq++;
            }
            else
            {
                sendAck(ACK_FAIL, seq);
            }
        }
        break;

    case CLOSE_FILE_CMD:
        {
            if (_onCloseFile)
                _onCloseFile();
            sendAck(ACK_OK, CMD_ACK_FLAG | CLOSE_FILE_CMD);
            Serial.println("[B] END");
        }
        break;

    case OPEN_IMG_CMD:
        {
            const uint8_t imgIndex = data[1];
            bool ok = false;
            if (_onOpenImg)
                ok = _onOpenImg(imgIndex);
            sendAck(ok ? ACK_OK : ACK_FAIL, CMD_ACK_FLAG | OPEN_IMG_CMD);
            Serial.printf("[B] OPEN idx=%hhu ok=%d\n", imgIndex, ok);
        }
        break;

    case CHANGE_BRIGHTNESS_CMD:
        {
            const uint8_t value = data[1];
            if (_onBrightnessChange)
                _onBrightnessChange(value);
            sendAck(ACK_OK, CMD_ACK_FLAG | CHANGE_BRIGHTNESS_CMD);
            Serial.printf("[B] BRIGHT %hhu\n", value);
        }
        break;
    case CHANGE_SPEED_CMD:
        {
            const uint8_t value = data[1];
            if (_onSpeedChange)
                _onSpeedChange(value);
            sendAck(ACK_OK, CMD_ACK_FLAG | CHANGE_SPEED_CMD);
            Serial.printf("[B] SPEED %hhu\n", value);
        }
    default:
        break;
    }
}

void BluetoothService::sendAck(uint8_t status, uint16_t seq) const
{
    uint8_t packet[3];
    packet[0] = status;
    packet[1] = (uint8_t)(seq & 0xFF);
    packet[2] = (uint8_t)((seq >> 8) & 0xFF);
    pTxCharacteristic->setValue(packet, 3);
    pTxCharacteristic->notify();
    Serial.printf("[B] ACK: %hhu, seq: %hhu \r\n", status, seq);
}

void MyCallbacks::onConnect(BLEServer* pServer)
{
    deviceConnected = true;
    Serial.println("[B] Connected");
    expectedSeq = 0;
}

void MyCallbacks::onDisconnect(BLEServer* pServer)
{
    deviceConnected = false;
    Serial.println("[B] Disconnected");
    pServer->getAdvertising()->start();
}


void MyRxCallbacks::onWrite(BLECharacteristic* pCharacteristic)
{
    std::string rxValue = pCharacteristic->getValue();
    if (rxValue.length() == 0)
        return;

    const uint8_t* data = reinterpret_cast<const uint8_t*>(rxValue.data());
    _service->receiveData(data, rxValue.length(), data[0]);
}
