namespace BLE
{
    public static class BLEConstants
    {
        public static byte COMMAND_IMG_START = 0x01;
        public static byte COMMAND_IMG_TRANSMIT = 0x02;
        public static byte COMMAND_IMG_END = 0x03;
        public static byte COMMAND_IMG_OPEN = 0x04;
        public static byte COMMAND_GET_BATTERY_CHARGE = 0x05;
        public static byte COMMAND_HARD_RESET = 0x06;
        public static byte COMMAND_SET_COLOR = 0x07;
        public static byte COMMAND_SET_BRIGHTNESS = 0x08;
        public static byte COMMAND_SET_SPEED = 0x09;
        
        public static string TARGET_UUID = "5E400001-B5A3-F393-E0A9-E50E24DCCA9E";
    }
    
}