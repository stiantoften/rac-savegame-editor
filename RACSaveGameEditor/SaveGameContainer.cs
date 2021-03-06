using System;
using System.IO;
using System.Text;
using PS3SaveManager;

namespace RACSaveGameEditor
{
    public abstract class SaveGameContainer
    {
        private byte[] fileData;
        public string path;
        public GameType type;
        
        protected SaveGameContainer(string path)
        {
            this.path = path;
        }

        public virtual int ReadInt(int position) {
            return (fileData[position + 0] << 24) | (fileData[position + 1] << 16) | (fileData[position + 2] << 8) | (fileData[position + 3]);
        }

        public virtual void WriteInt(int position, int value) {
            byte[] byt = BitConverter.GetBytes(value);
            fileData[position + 0] = byt[3];
            fileData[position + 1] = byt[2];
            fileData[position + 2] = byt[1];
            fileData[position + 3] = byt[0];
        }

        public virtual byte ReadByte(int position) {
            return fileData[position];
        }

        public virtual void WriteByte(int position, byte value) {
            fileData[position] = value;
        }

        public abstract void Load();
        public abstract void Save();

        public static GameType DetectPS3GameType(string region)
        {
            Console.WriteLine("Attempting to match region {0}", region);
            switch (region)
            {
                case "NPEA00385": case "NPUA80643": case "NPJA40001":
                    return GameType.RAC;
                case "NPEA00386": case "NPUA80644": case "NPJA40002":
                    return GameType.GC;
                case "NPEA00387": case "NPUA80645": case "NPJA40003":
                    return GameType.UYA;
                case "NPEA00423": // TODO: add other regions
                    return GameType.DL;
                case "BCUS98127": case "BCES00052": case "BCKS10016":
                case "BCJS30014": case "BCJS70012": case "BCKS10054":
                case "BCAS20045": case "BCJS70004": case "NPUA98153":
                case "NPEA90017": case "NPJA90035": case "NPHA20002":
                    return GameType.TOD;
                case "BCUS98187": case "NPUA80145": case "BLES00301": 
                case "NPEA00088": case "BCES00301":
                    return GameType.QFB;
                case "BCUS98124": case "BCES00142": case "BCES00511":
                case "BCES00748": case "BCES00726": case "BCJS30038":
                case "BCKS10087": case "BCAS20098":
                    return GameType.ACIT;
            }

            return GameType.ERROR;
        }

        public static string GetPS3FileName(GameType type) {
            if (type == GameType.RAC || type == GameType.GC || type == GameType.UYA || type == GameType.DL) {
                return "USR-DATA";
            } else {
                return "GAME.SAV";
            }
        }

        public class EncryptedPS3 : SaveGameContainer
        {
            private Ps3SaveManager manager;

            public EncryptedPS3(string path) : base(path)
            {
                string region;
                using (var stream = new FileStream(Path.Combine(path, "PARAM.SFO"), FileMode.Open, FileAccess.Read))
                {
                    var buffer = new byte[9];
                    stream.Seek(0x968, SeekOrigin.Begin);
                    stream.Read(buffer, 0, 9);
                    region = Encoding.ASCII.GetString(buffer);
                }
                    
                type = DetectPS3GameType(region);
                switch (type)
                {
                    case GameType.RAC:
                        manager = new Ps3SaveManager(path, new byte[] {
                            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0xFA, 0xCB, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10});
                        break;
                    case GameType.GC:
                        manager = new Ps3SaveManager(path, new byte[] {
                            0xC0, 0xA3, 0xB3, 0x64, 0x1C, 0x2A, 0xD1, 0xEF, 0x23, 0x15, 0x3A, 0x48, 0xA3, 0xE1, 0x2F, 0xE8});
                        break;
                    case GameType.UYA:
                        manager = new Ps3SaveManager(path, new byte[] {
                            0xC0, 0xA3, 0xB3, 0x64, 0x1C, 0x2A, 0xD1, 0xEF, 0x23, 0x15, 0x3A, 0x48, 0xA3, 0xE1, 0x2F, 0xE7});
                        break;
                    case GameType.QFB:
                        manager = new Ps3SaveManager(path, new byte[] {
                            0xC0, 0xA3, 0xB3, 0x64, 0x1C, 0x2A, 0xD1, 0xEF, 0x23, 0x15, 0x3A, 0x48, 0xA3, 0xE1, 0x2F, 0xE7});
                        break;
                    
                    default:
                        manager = new Ps3SaveManager(path, new byte[] {
                            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF});
                        break;
                }
            }

            public override void Load()
            {
                var fileName = GetPS3FileName(type);

                foreach (var saveFile in manager.Files)
                {
                    if (saveFile.PFDEntry.file_name == fileName)
                    {
                        fileData = saveFile.DecryptToBytes();
                    }
                }
            }

            public override void Save()
            {
                var fileName = GetPS3FileName(type);

                foreach (var saveFile in manager.Files)
                {
                    if (saveFile.PFDEntry.file_name == fileName)
                    {
                        saveFile.Encrypt(fileData);
                    }
                }

                manager.ReBuildChanges();
            }
        }
        
        public class DecryptedPS3 : SaveGameContainer
        {
            public DecryptedPS3(string path) : base(path)
            {
                this.path = path;

                
                string region = "";
                try {
                    region = Path.GetFileName(path).Split('_')[0];
                } catch {}

                type = DetectPS3GameType(region);

                if (type == GameType.ERROR) {
                    using (var stream = new FileStream(Path.Combine(path, "PARAM.SFO"), FileMode.Open, FileAccess.Read))
                    {
                        var buffer = new byte[9];
                        stream.Seek(0x968, SeekOrigin.Begin);
                        //stream.Seek(0x5d8, SeekOrigin.Begin);
                        stream.Read(buffer, 0, 9);
                        region = Encoding.ASCII.GetString(buffer);
                    }
                    type = DetectPS3GameType(region);
                }
            }

            public override void Load()
            {
                var fileName = GetPS3FileName(type);
                fileData = File.ReadAllBytes(Path.Combine(path, fileName));
            }

            public override void Save()
            {
                var fileName = GetPS3FileName(type);
                File.WriteAllBytes(Path.Combine(path, fileName), fileData);
            }
        }

        public static GameType DetectPSVGameType(string prefix)
        {
            Console.WriteLine("[PSV] Attempting to match prefix {0}", prefix);
            switch (prefix)
            {
                case "RC1":
                    return GameType.RAC;
                case "RC2":
                    return GameType.GC;
                case "RC3":
                    return GameType.UYA;
            }

            return GameType.ERROR;
        }

        public class DecryptedPSV : SaveGameContainer
        {
            public DecryptedPSV(string path) : base(path)
            {
                this.path = path;


                string prefix = "";
                try
                {
                    prefix = Path.GetFileName(path).Split('_')[0];
                }
                catch { }

                type = DetectPSVGameType(prefix);

                if (type == GameType.ERROR)
                {
                    Console.WriteLine("[PSV] Couldn't detect game version"); 
                    //Might be possible to detect which game it is by analyzing header
                    //Needs more research
                }
            }

            public override void Load()
            {
                fileData = File.ReadAllBytes(path);
            }

            public override void Save()
            {
                File.WriteAllBytes(path, fileData);
            }

            public override int ReadInt(int position) {
                // TODO: Test if this works as intended and remove this message if so.
                return (fileData[position + 3] << 24) | (fileData[position + 2] << 16) | (fileData[position + 1] << 8) | (fileData[position + 0]);
            }

            public override void WriteInt(int position, int value) {
                byte[] byt = BitConverter.GetBytes(value);
                fileData[position + 0] = byt[0];
                fileData[position + 1] = byt[1];
                fileData[position + 2] = byt[2];
                fileData[position + 3] = byt[3];
            }

        }

        public static GameType DetectPS2GameType(string prefix)
        {
            Console.WriteLine("[PS2] Attempting to match {0}", prefix);
            switch (prefix)
            {
                case "SCES-50916": case "SCUS-97199": case "SCAJ-20001": 
                case "SCKA-20120": case "SCKA-15037": case "SCKA-19211":
                case "SCKA-19316":
                    return GameType.RAC;
                case "SCES-51607": case "SCUS-97268": case "SCUS-97513":
                case "SCKA-20011": case "SCPS-15056": case "SCPS-19302":
                case "SCPS-19317":
                    return GameType.GC;
                case "SCES-52456": case "SCUS-97353": case "SCUS-97518":
                case "SCAJ-20109": case "SCKA-20037": case "SCPS-19309":
                case "SCPS-15084":
                    return GameType.UYA;
                case "SCES-53285": case "SCUS-97465": case "SCAJ-20157":
                case "SCKA-20060": case "SCPS-15100": case "SCPS-15099":
                case "SCPS-19321": case "SCPS-19328":
                    return GameType.DL;
            }

            return GameType.ERROR;
        }

        public class PS2BinFile : SaveGameContainer
        {
            public PS2BinFile(string path) : base(path)
            {
                this.path = path;
                string dirname = Path.GetFileName(Path.GetDirectoryName(path));
                type = DetectPS2GameType(dirname.Substring(0, dirname.Length - 7).Substring(2));

                if (type == GameType.ERROR)
                {
                    Console.WriteLine("[PS2] Couldn't detect game version"); 
                }
            }

            public override void Load()
            {
                fileData = File.ReadAllBytes(path);
            }

            public override void Save()
            {
                // TODO: Enable this and implement checksum re-calculation.
                // File.WriteAllBytes(path, fileData);
            }

            public override int ReadInt(int position) {
                // TODO: Test if this works as intended and remove this message if so.
                return (fileData[position + 3] << 24) | (fileData[position + 2] << 16) | (fileData[position + 1] << 8) | (fileData[position + 0]);
            }

            public override void WriteInt(int position, int value) {
                byte[] byt = BitConverter.GetBytes(value);
                fileData[position + 0] = byt[0];
                fileData[position + 1] = byt[1];
                fileData[position + 2] = byt[2];
                fileData[position + 3] = byt[3];
            }

        }
    }
}