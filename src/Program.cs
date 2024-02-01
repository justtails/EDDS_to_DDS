﻿using System.Text;
using K4os.Compression.LZ4.Encoders;

namespace EddsToDds
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                switch (args[0])
                {
                    case "help":
                        WriteHelp();
                        break;
                    default:
                        OpenFile(args[0]);
                        break;
                }
            }
            catch
            {
                WriteHelp();
            }
        }

        static void WriteHelp()
        {
            Console.WriteLine("EddsToDds [help | filepath] \n\n" +
                                "help\t\tShow this help\n" +
                                "filepath\tPath to edds file");
        }

        static void OpenFile(string file)
        {
            List<int> copy_blocks = new List<int>();
            //List<int> LZO_blocks = new List<int>(); TODO later
            List<int> LZ4_blocks = new List<int>();
            List<byte> Decoded_blocks = new List<byte>();

            void FindBlocks(BinaryReader reader)
            {
                while (true)
                {
                    byte[] blocks = reader.ReadBytes(4);

                    char[] dd = Encoding.UTF8.GetChars(blocks);

                    string block = new string(dd);
                    int size = reader.ReadInt32();

                    switch (block)
                    {
                        case "COPY": copy_blocks.Add(size); break;
                        case "LZ4 ": LZ4_blocks.Add(size); break;
                        default: reader.BaseStream.Seek(-8, SeekOrigin.Current); return;
                    }
                }
            }

            using (var reader = new BinaryReader(File.Open(file, FileMode.Open)))
            {
                byte[] dds_header = reader.ReadBytes(128);
                byte[] dds_header_dx10 = null;

                if(dds_header[84]=='D'&& dds_header[85] == 'X' && dds_header[86] == '1' && dds_header[87] == '0')
                {
                    dds_header_dx10 = reader.ReadBytes(20);
                }

                FindBlocks(reader);

                foreach (int count in copy_blocks)
                {
                    byte[] buff = reader.ReadBytes(count);
                    Decoded_blocks.InsertRange(0, buff);
                }

                foreach (int Length in LZ4_blocks)
                {


                    uint size = reader.ReadUInt32();
                    byte[] target = new byte[size];

                    int num = 0;
                    LZ4ChainDecoder lz4ChainDecoder = new LZ4ChainDecoder(65536, 0);
                    int count1;
                    int idx = 0;
                    for (; num < Length - 4; num += (count1 + 4))
                    {
                        count1 = reader.ReadInt32() & int.MaxValue;
                        byte[] numArray = reader.ReadBytes(count1);
                        byte[] buffer = new byte[65536];
                        int count2 = 0;
                        LZ4EncoderExtensions.DecodeAndDrain((ILZ4Decoder)lz4ChainDecoder, numArray, 0, count1, buffer, 0, 65536, out count2);

                        Array.Copy(buffer, 0, target, idx, count2);

                        idx += count2;
                    }

                    Decoded_blocks.InsertRange(0, target);
                }
                if(dds_header_dx10!= null)
                    Decoded_blocks.InsertRange(0, dds_header_dx10);
                Decoded_blocks.InsertRange(0, dds_header);
                byte[] final = Decoded_blocks.ToArray();

                using (var wr = File.Create(Path.GetFileNameWithoutExtension(file) +".dds"))
                {
                    wr.Write(final, 0, final.Length);
                }
            }

            
        }


    }
}
