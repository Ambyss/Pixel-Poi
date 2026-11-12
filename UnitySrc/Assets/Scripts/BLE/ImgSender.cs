using System;
using System.Collections.Generic;
using System.Linq;
using MEC;
using UnityEngine;
using Zenject;

namespace BLE
{
    public class ImgSender
    {
        [Inject] private BLESender _sender;

        // [opcode][seq lo][seq hi] in front of every data chunk.
        private const int CHUNK_HEADER = 3;
        private const int MAX_RETRIES = 3;
        private const float ACK_TIMEOUT = 0.5f;
        private const float POLL_INTERVAL = 0.01f;
        private const int CMD_ACK_FLAG = 0x8000;

        private static int CommandAckKey(byte opcode) => CMD_ACK_FLAG | opcode;

        public Action OnUploadComplete;
        public Action<string> OnUploadFailed;
        public Action<float> OnUploadProgress;

        public void SendImage(byte[] imageBinaryData, int imgIndex)
        {
            if (imageBinaryData == null || imageBinaryData.Length == 0)
            {
                OnUploadFailed?.Invoke("Empty image data");
                return;
            }
            Timing.RunCoroutine(SendImageCoroutine(imgIndex, imageBinaryData));
        }

        public void OpenImage(int imgIndex)
        {
            Timing.RunCoroutine(OpenImageCoroutine(imgIndex));
        }

        private IEnumerator<float> OpenImageCoroutine(int imgIndex)
        {
            List<byte> openPacket = new List<byte>
            {
                BLEConstants.COMMAND_IMG_OPEN,
                (byte)imgIndex
            };
            bool openOk = false;
            yield return Timing.WaitUntilDone(Timing.RunCoroutine(
                SendWithAck(openPacket, CommandAckKey(BLEConstants.COMMAND_IMG_OPEN), r => openOk = r)));
            
            if (!openOk)
            {
                OnUploadFailed?.Invoke("OPEN not acknowledged");
            }
        }
        
        private IEnumerator<float> SendImageCoroutine(int imgIndex, byte[] data)
        {
            List<byte> startPacket = new List<byte>
            {
                BLEConstants.COMMAND_IMG_START,
                (byte)imgIndex
            };
            startPacket.AddRange(BitConverter.GetBytes(data.Length));

            bool startOk = false;
            yield return Timing.WaitUntilDone(Timing.RunCoroutine(
                SendWithAck(startPacket, CommandAckKey(BLEConstants.COMMAND_IMG_START), r => startOk = r)));

            if (!startOk)
            {
                OnUploadFailed?.Invoke("START not ACK");
                yield break;
            }

            // Sized from the negotiated MTU, so a raised MTU directly means fewer ACK round
            // trips. Read once per upload: it must not change halfway through a file.
            int payload = _sender.MaxWriteLength - CHUNK_HEADER;
            int totalChunks = (int)Math.Ceiling((double)data.Length / payload);
            Debug.Log($"Total chunks: {totalChunks}, data length: {data.Length}, payload: {payload} B");

            for (int i = 0; i < totalChunks; i++)
            {
                int offset = i * payload;
                int size = Math.Min(payload, data.Length - offset);
                ushort seq = (ushort)i;

                var chunk = new List<byte> { BLEConstants.COMMAND_IMG_TRANSMIT };
                chunk.Add((byte)(seq & 0xFF));
                chunk.Add((byte)((seq >> 8) & 0xFF));
                chunk.AddRange(data.Skip(offset).Take(size));

                bool chunkOk = false;
                yield return Timing.WaitUntilDone(Timing.RunCoroutine(
                    SendWithAck(chunk, seq, r => chunkOk = r)));

                if (!chunkOk)
                {
                    OnUploadFailed?.Invoke($"Chunk {i + 1}/{totalChunks} failed after {MAX_RETRIES} retries");
                    yield break;
                }

                OnUploadProgress?.Invoke((float)(i + 1) / totalChunks);
            }

            List<byte> endPacket = new List<byte> { BLEConstants.COMMAND_IMG_END };
            bool endOk = false;
            yield return Timing.WaitUntilDone(Timing.RunCoroutine(
                SendWithAck(endPacket, CommandAckKey(BLEConstants.COMMAND_IMG_END), r => endOk = r)));

            if (!endOk)
            {
                OnUploadFailed?.Invoke("END not acknowledged");
                yield break;
            }

            Timing.WaitUntilDone(OpenImageCoroutine(imgIndex));

            OnUploadComplete?.Invoke();
            Debug.Log("Upload complete");
        }

        private IEnumerator<float> SendWithAck(List<byte> packet, int ackKey, Action<bool> onResult)
        {
            for (int attempt = 0; attempt < MAX_RETRIES; attempt++)
            {
                _sender.Ack.BeginWait(ackKey);
                _sender.Transmit(packet);

                float elapsed = 0f;
                AckResult result = AckResult.None;

                while (elapsed < ACK_TIMEOUT)
                {
                    result = _sender.Ack.Poll(ackKey);
                    if (result != AckResult.None)
                        break;

                    yield return Timing.WaitForSeconds(POLL_INTERVAL);
                    elapsed += POLL_INTERVAL;
                }

                if (result == AckResult.Ok)
                {
                    onResult(true);
                    yield break;
                }

                Debug.LogWarning($"ACK key {ackKey}: {(result == AckResult.Fail ? "FAIL" : "TIMEOUT")}, attempt {attempt + 1}/{MAX_RETRIES}");
            }

            onResult(false);
        }
    }
}
