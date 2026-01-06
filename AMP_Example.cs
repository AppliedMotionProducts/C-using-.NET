using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AppliedMotionDemo
{
    // Helper class to store UDP state for async callback
    class UdpState
    {
        public UdpClient Client { get; set; }
        public IPEndPoint EndPoint { get; set; } = new IPEndPoint(IPAddress.Any, 0);
    }

    public class AmpDrive
    {
        private UdpClient udpClient;

        public AmpDrive(string driveIp, int localPort = 7777, int remotePort = 7775)
        {
            udpClient = new UdpClient(localPort);
            udpClient.Connect(driveIp, remotePort);
        }

        // Send an SCL command, e.g., "RV"
        public void SendCommand(string command)
        {
            byte[] sclBytes = Encoding.ASCII.GetBytes(command);
            byte[] sendBytes = new byte[sclBytes.Length + 3];

            // Opcode 07 is used for SCL commands
            sendBytes[0] = 0;
            sendBytes[1] = 7;

            Array.Copy(sclBytes, 0, sendBytes, 2, sclBytes.Length);

            // Terminator (CR)
            sendBytes[sendBytes.Length - 1] = 13;

            udpClient.Send(sendBytes, sendBytes.Length);
            Console.WriteLine($"Sent command: {command}");
        }

        // Send opcode 99 (ping)
        public void SendPing()
        {
            byte[] pingBytes = new byte[2];
            int opcode = 99;
            pingBytes[0] = (byte)(opcode / 256); // high byte
            pingBytes[1] = (byte)(opcode % 256); // low byte

            udpClient.Send(pingBytes, pingBytes.Length);
            Console.WriteLine("Sent opcode 99 (Ping) to drive");
        }

        // Start async receive
        public void StartAsyncReceive()
        {
            UdpState state = new UdpState
            {
                Client = udpClient
            };
            udpClient.BeginReceive(ReceiveCallback, state);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            UdpState state = (UdpState)ar.AsyncState!;

            IPEndPoint localEP = state.EndPoint;
            byte[] receiveBytes = state.Client.EndReceive(ar, ref localEP);

            if (receiveBytes.Length < 2)
            {
                Console.WriteLine("Received packet too short to process.");
                StartAsyncReceive();
                return;
            }

            int opcode = 256 * receiveBytes[0] + receiveBytes[1];

            switch (opcode)
            {
                case 7: // SCL response
                    {
                        byte[] sclBytes = new byte[receiveBytes.Length - 2];
                        Array.Copy(receiveBytes, 2, sclBytes, 0, sclBytes.Length);
                        string response = Encoding.ASCII.GetString(sclBytes);
                        Console.WriteLine($"Async received (SCL): {response}");
                        break;
                    }

                case 99: // Ping response
                    {
                        // Print as hex bytes (safe for binary data)
                        Console.WriteLine("Ping response received (hex): " + BitConverter.ToString(receiveBytes));

                        // Optional: parse numeric response if drive sends a known format
                        if (receiveBytes.Length >= 6)
                        {
                            int value = BitConverter.ToInt32(receiveBytes, 2); // bytes 2-5
                            Console.WriteLine($"Ping response value: {value}");
                        }

                        break;
                    }

                default:
                    Console.WriteLine($"Received unknown opcode {opcode}, {receiveBytes.Length} bytes");
                    break;
            }

            // Re-register callback to keep receiving
            state.EndPoint = localEP;
            StartAsyncReceive();
        }
    }

    class Program
    {
        static void Main()
        {
            string driveIp = "10.10.10.10"; // <- your drive IP

            AmpDrive drive = new AmpDrive(driveIp);

            // Start async receive first
            drive.StartAsyncReceive();

            // Send SCL command example
            drive.SendCommand("SC");

            // Send ping
            drive.SendPing();

            Console.WriteLine("Press Enter to quit...");
            Console.ReadLine();
        }
    }
}
