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

            //Get the starting position of the item 
            item.Position = c.BaseStream.Position;
            //Read all bytes of the item (items end in 0xDB, 0xBF, 0xEF, 0x19, 0x10)
            item.Bytes = ReadToPattern(c, new byte[5] { 0xDB, 0xBF, 0xEF, 0x19, 0x10 });

            //Read the bytes using a binary reader
            using (BinaryReader b = new BinaryReader(new MemoryStream(item.Bytes)))
            {
                byte ItemLength = b.ReadByte();
                b.ReadByte(); //Always 0
                item.Name = Encoding.ASCII.GetString(b.ReadBytes(ItemLength));
                b.ReadBytes(5); //Always E0 AC 91 8E 10
                byte CategoryLength = b.ReadByte();
                b.ReadByte(); //Always 0
                item.Category = Encoding.ASCII.GetString(b.ReadBytes(CategoryLength));

                //This is still broken ish - just my guess! Seems to kind of work
                ReadToPattern(b, new byte[4] { 0xBC, 0xF0, 0x8F, 0x0D });
                b.ReadBytes(2);
                byte x1 = b.ReadByte();
                b.ReadBytes(3);
                byte y1 = b.ReadByte();
                b.ReadBytes(3);
                byte x2 = b.ReadByte();
                b.ReadBytes(3);
                byte y2 = b.ReadByte();

                //set the item data to be manipulated later
                item.X = new Tuple<byte, byte>(x1, x2);
                item.Y = new Tuple<byte, byte>(y1, y2);
            }
            return item;
        }

        /// <summary>
        /// Replace the HUD co-ordinates with the user supplied ones
        /// </summary>
        /// <param name="x">The tuple containing the width and x</param>
        /// <param name="y">The tuple containing with height and y</param>
        /// <returns></returns>
        public byte[] ReplaceCoordinates(Tuple<byte, byte> x, Tuple<byte, byte> y)
        {
            //Use ReallyBadHack to find the location of the co-ordinates
            long ReallyBadHack = 0;
            using (BinaryReader b = new BinaryReader(new MemoryStream(Bytes)))
            {
                //Read to the co-ordinate location and set the position
                ReadToPattern(b, new byte[4] { 0xBC, 0xF0, 0x8F, 0x0D });
                ReallyBadHack = b.BaseStream.Position;
            }

            //Modify the bytes to the user supplied bytes
            byte[] FinalBytes = (byte[])Bytes.Clone();
            FinalBytes[ReallyBadHack + 2] = x.Item1;
            FinalBytes[ReallyBadHack + 6] = y.Item1;
            FinalBytes[ReallyBadHack + 10] = x.Item2;
            FinalBytes[ReallyBadHack + 14] = y.Item2;

            return FinalBytes;
        }

        /// <summary>
        /// Reads to a specified pattern. Exceptions if at end of data
        /// </summary>
        /// <param name="b">The binary reader to read from</param>
        /// <param name="arr">The byte array containing the pattern to match</param>
        /// <returns>A byte array containing all data from the original position to the pattern position</returns>
        private static byte[] ReadToPattern(BinaryReader b, byte[] arr)
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