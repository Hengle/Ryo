namespace Ryo.Reloaded.CRI;

public static class CriWareConfig
{
    public static int AcbNameOffset { get; private set; } = 0x98;
    
    public static int HcaDecodedEncryptKeyOffset { get; set; } = 0x120;
    
    public static void SetAtomExVersion(Version atomExVer)
    {
        // Name PTR shifted by 8.
        // Games:
        // Digimon Story: Time Stranger / 2.28.17
        // Sonic Racing CrossWorlds / 2.28.17
        if (atomExVer >= new Version("2.28.17"))
        {
            AcbNameOffset = 0x98 + 0x8;
            //HcaDecodedEncryptKeyOffset = 0xb0;
        }
        
        Log.Information($"Cri AtomEx Version: {atomExVer}");
    }
}