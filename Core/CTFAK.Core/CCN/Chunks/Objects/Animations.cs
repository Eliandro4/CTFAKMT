using System.Collections.Generic;
using System.IO;
using CTFAK.Memory;
using CTFAK.Utils;

namespace CTFAK.CCN.Chunks.Objects
{
    public class Animations : ChunkLoader
    {
        public Dictionary<int, Animation> AnimationDict;

        public override void Read(ByteReader reader)
        {
            var currentPosition = reader.Tell();
            var size = reader.ReadInt16();
            var count = reader.ReadInt16();

            var offsets = new List<short>();
            for (int i = 0; i < count; i++)
            {
                offsets.Add(reader.ReadInt16());
            }
            AnimationDict = new Dictionary<int, Animation>();
            for (int i = 0; i < offsets.Count; i++)
            {
                var offset = offsets[i];
                if (offset != 0)
                {
                    reader.Seek(currentPosition + offset);
                    var anim = new Animation();

                    anim.Read(reader);
                    AnimationDict.Add(i, anim);
                }
                else
                {
                    AnimationDict.Add(i, new Animation());
                }
            }
        }

        public override void Write(ByteWriter writer)
        {
            var count = AnimationDict.Count;
            var animOffsets = new int[count];
            var animData = new ByteWriter(new MemoryStream());

            for (int i = 0; i < count; i++)
            {
                animOffsets[i] = (int)animData.Tell();
                var animWriter = new ByteWriter(new MemoryStream());
                if (AnimationDict.ContainsKey(i))
                    AnimationDict[i].Write(animWriter);
                animData.WriteWriter(animWriter);
            }

            writer.WriteInt16((short)(4 + count * 2 + animData.ToArray().Length));
            writer.WriteInt16((short)count);
            var baseOffset = 4 + count * 2;
            for (int i = 0; i < count; i++)
                writer.WriteInt16((short)(baseOffset + animOffsets[i]));
            writer.WriteWriter(animData);
        }
    }

    public class Animation : ChunkLoader
    {
        public Dictionary<int, AnimationDirection> DirectionDict;


        public override void Read(ByteReader reader)
        {
            var currentPosition = reader.Tell();
            var offsets = new List<int>();
            for (int i = 0; i < 32; i++)
            {
                offsets.Add(reader.ReadInt16());
            }

            DirectionDict = new Dictionary<int, AnimationDirection>();
            for (int i = 0; i < offsets.Count; i++)
            {
                var offset = offsets[i];
                if (offset != 0)
                {
                    reader.Seek(currentPosition + offset);
                    var dir = new AnimationDirection();
                    dir.Read(reader);
                    DirectionDict.Add(i, dir);
                }
                else
                {
                    DirectionDict.Add(i, new AnimationDirection());
                }

            }
        }

        public override void Write(ByteWriter writer)
        {
            var dirOffsets = new int[32];
            var dirData = new ByteWriter(new MemoryStream());

            for (int i = 0; i < 32; i++)
            {
                dirOffsets[i] = 0;
                if (DirectionDict.ContainsKey(i))
                {
                    dirOffsets[i] = (int)dirData.Tell();
                    var dirWriter = new ByteWriter(new MemoryStream());
                    DirectionDict[i].Write(dirWriter);
                    dirData.WriteWriter(dirWriter);
                }
            }

            var baseOffset = 32 * 2;
            for (int i = 0; i < 32; i++)
                writer.WriteInt16((short)(baseOffset + dirOffsets[i]));
            writer.WriteWriter(dirData);
        }



    }

    public class AnimationDirection : ChunkLoader
    {
        public int MinSpeed;
        public int MaxSpeed;
        public bool HasSingle;
        public int Repeat;
        public int BackTo;
        public List<int> Frames = new List<int>();



        public override void Read(ByteReader reader)
        {
            MinSpeed = reader.ReadSByte();
            MaxSpeed = reader.ReadSByte();
            Repeat = reader.ReadInt16();
            BackTo = reader.ReadInt16();
            var frameCount = reader.ReadUInt16();
            for (int i = 0; i < frameCount; i++)
            {
                var handle = reader.ReadInt16();
                Frames.Add(handle);


            }


        }

        public override void Write(ByteWriter writer)
        {
            writer.WriteUInt8((sbyte)MinSpeed);
            writer.WriteUInt8((sbyte)MaxSpeed);
            writer.WriteInt16((short)Repeat);
            writer.WriteInt16((short)BackTo);
            writer.WriteUInt16((ushort)Frames.Count);
            foreach (var handle in Frames)
                writer.WriteInt16((short)handle);
        }


    }
}