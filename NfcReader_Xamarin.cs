using System;
using Plugin.NFC;

public class NfcReader
{
    public async Task ReadNfcTagAsync()
    {
        // Check if NFC is available and enabled
        if (!CrossNFC.IsSupported || !CrossNFC.Current.IsEnabled)
        {
            Console.WriteLine("NFC is not supported or not enabled.");
            return;
        }

        // Subscribe to NFC events
        CrossNFC.Current.OnMessageReceived += Current_OnMessageReceived;
        CrossNFC.Current.OnTagDiscovered += Current_OnTagDiscovered;

        Console.WriteLine("Waiting for NFC tag...");

        // Start listening for NFC tags
        await CrossNFC.Current.StartListeningAsync();
    }

    private void Current_OnTagDiscovered(ITagInfo tagInfo, bool format)
    {
        Console.WriteLine("NFC tag detected.");
    }

    private void Current_OnMessageReceived(NdefMessage message)
    {
        Console.WriteLine("Message received.");

        // Read NDEF records from the NFC tag
        foreach (var record in message.Records)
        {
            Console.WriteLine("Type: " + record.Type);
            Console.WriteLine("Payload: " + BitConverter.ToString(record.Payload));
        }

        Console.WriteLine("NFC tag read complete.");
    }
}
