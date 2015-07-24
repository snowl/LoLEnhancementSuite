using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace LESs
{
    public class HUDItem
    {
        public string Name { get; set; }
        public string Category { get; set; }

        public Tuple<byte, byte> X { get; set; }
        public Tuple<byte, byte> Y { get; set; }

        public byte[] Bytes { get; set; }
        public long Position { get; set; }
        
        public static HUDItem Read(BinaryReader c)
        {
            HUDItem item = new HUDItem();
            item.Position = c.BaseStream.Position;
            item.Bytes = ReadToPattern(c, new byte[5] { 0xDB, 0xBF, 0xEF, 0x19, 0x10 });

            using (BinaryReader b = new BinaryReader(new MemoryStream(item.Bytes)))
            {
                byte ItemLength = b.ReadByte();
                b.ReadByte(); //Always 0
                item.Name = Encoding.ASCII.GetString(b.ReadBytes(ItemLength));
                b.ReadBytes(5); //Always E0 AC 91 8E 10
                byte CategoryLength = b.ReadByte();
                b.ReadByte(); //Always 0
                item.Category = Encoding.ASCII.GetString(b.ReadBytes(CategoryLength));

                ReadToPattern(b, new byte[4] { 0xBC, 0xF0, 0x8F, 0x0D });
                b.ReadBytes(2);
                byte x1 = b.ReadByte();
                b.ReadBytes(3);
                byte y1 = b.ReadByte();
                b.ReadBytes(3);
                byte x2 = b.ReadByte();
                b.ReadBytes(3);
                byte y2 = b.ReadByte();

                item.X = new Tuple<byte, byte>(x1, x2);
                item.Y = new Tuple<byte, byte>(y1, y2);
            }
            return item;
        }

        public byte[] ReplaceCoordinates(Tuple<byte, byte> x, Tuple<byte, byte> y)
        {
            long ReallyBadHack = 0;
            using (BinaryReader b = new BinaryReader(new MemoryStream(Bytes)))
            {
                ReadToPattern(b, new byte[4] { 0xBC, 0xF0, 0x8F, 0x0D });
                ReallyBadHack = b.BaseStream.Position;
            }

            byte[] FinalBytes = (byte[])Bytes.Clone();
            FinalBytes[ReallyBadHack + 2] = x.Item1;
            FinalBytes[ReallyBadHack + 6] = y.Item1;
            FinalBytes[ReallyBadHack + 10] = x.Item2;
            FinalBytes[ReallyBadHack + 14] = y.Item2;

            return FinalBytes;
        }

        public static byte[] ReadToPattern(BinaryReader b, byte[] arr)
        {
            List<byte> _bytes = new List<byte>();

            while (true)
            {
                byte c = b.ReadByte();
                _bytes.Add(c);

                if (c == arr[0])
                {
                    bool match = true;
                    byte[] extra = b.ReadBytes(arr.Length - 1);
                    _bytes.AddRange(extra);

                    for (int i = 0; i < extra.Length; i++)
                    {
                        if (extra[i] != arr[i + 1])
                        {
                            match = false;
                        }
                    }

                    if (match == true)
                        return _bytes.ToArray();
                }
            }
        }

    }
}