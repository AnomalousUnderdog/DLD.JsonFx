#region License

/*---------------------------------------------------------------------------------*\

	Distributed under the terms of an MIT-style license:

	The MIT License

	Copyright (c) 2006-2010 Stephen M. McKamey

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in
	all copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
	THE SOFTWARE.

\*---------------------------------------------------------------------------------*/

#endregion License

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DLD.JsonFx
{
	/// <summary>
	/// Writer for producing JSON data like <see cref="JsonWriter"/>, but also sends that same data to a
	/// <see cref="System.Security.Cryptography.HashAlgorithm"/>
	/// to output a hash code at the end.
	/// </summary>
	/// <seealso cref="HashGenerator"/>
	public class JsonWriterWithHasher : JsonWriter
	{
		#region Fields

		// from https://stackoverflow.com/a/3621316
		protected readonly HashAlgorithm Hasher;
		protected readonly byte[] HasherByteBuffer = new byte[16];

		#endregion Fields

		#region Init

		public JsonWriterWithHasher(TextWriter output, JsonWriterSettings settings, string hashAlgorithm) : base(output,
			settings)
		{
			Hasher = HashAlgorithm.Create(hashAlgorithm);
			Hasher?.Initialize();
		}

		public JsonWriterWithHasher(string outputFileName, JsonWriterSettings settings, string hashAlgorithm) : base(
			outputFileName, settings)
		{
#if WINDOWS_STORE && !DEBUG
			throw new System.NotSupportedException ("Not supported on this platform");
#else
			Hasher = HashAlgorithm.Create(hashAlgorithm);
			Hasher?.Initialize();
#endif
		}

		#endregion Init

		#region Public Methods

		public override void Write(string value)
		{
			base.Write(value);

			if (value == null)
			{
				// "null" string value was written
				return;
			}

			byte[] stringBytes = Encoding.UTF8.GetBytes(value);
			Hasher.TransformBlock(stringBytes, 0, stringBytes.Length, null, 0);
		}

		public byte[] EndHash()
		{
			Hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
			return Hasher.Hash;
		}

		#endregion Public Methods

		#region Primative Writer Methods

		public override void Write(bool value)
		{
			base.Write(value);

			HasherByteBuffer[0] = value ? (byte)1 : (byte)0;
			Hasher.TransformBlock(HasherByteBuffer, 0, 1, null, 0);
		}

		public override void Write(byte value)
		{
			base.Write(value);

			HasherByteBuffer[0] = value;
			Hasher.TransformBlock(HasherByteBuffer, 0, 1, null, 0);
		}

		public override void Write(sbyte value)
		{
			base.Write(value);

			byte[] bytes = BitConverter.GetBytes(value);
			Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
		}

		public override void Write(short value)
		{
			base.Write(value);

			unchecked
			{
				HasherByteBuffer[0] = (byte)(value >> 8);
				HasherByteBuffer[1] = (byte)value;

				Hasher.TransformBlock(HasherByteBuffer, 0, 2, null, 0);
			}
		}

		public override void Write(ushort value)
		{
			base.Write(value);

			unchecked
			{
				HasherByteBuffer[0] = (byte)(value >> 8);
				HasherByteBuffer[1] = (byte)value;

				Hasher.TransformBlock(HasherByteBuffer, 0, 2, null, 0);
			}
		}

		public override void Write(int value)
		{
			base.Write(value);

			unchecked
			{
				HasherByteBuffer[0] = (byte)(value >> 24);
				HasherByteBuffer[1] = (byte)(value >> 16);
				HasherByteBuffer[2] = (byte)(value >> 8);
				HasherByteBuffer[3] = (byte)value;

				Hasher.TransformBlock(HasherByteBuffer, 0, 4, null, 0);
			}
		}

		public override void Write(uint value)
		{
			base.Write(value);
			if (InvalidIeee754(value))
			{
				// was written as string
				return;
			}

			byte[] bytes = BitConverter.GetBytes(value);
			Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
		}

		public override void Write(long value)
		{
			base.Write(value);
			if (InvalidIeee754(value))
			{
				// was written as string
				return;
			}

			byte[] bytes = BitConverter.GetBytes(value);
			Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
		}

		public override void Write(ulong value)
		{
			base.Write(value);
			if (InvalidIeee754(value))
			{
				// was written as string
				return;
			}

			byte[] bytes = BitConverter.GetBytes(value);
			Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
		}

		public override void Write(float value)
		{
			base.Write(value);

			if (float.IsNaN(value) || float.IsInfinity(value))
			{
				// "null" string value was written
				return;
			}

			byte[] bytes = BitConverter.GetBytes(value);
			Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
		}

		public override void Write(double value)
		{
			base.Write(value);

			if (double.IsNaN(value) || double.IsInfinity(value))
			{
				// "null" string value was written
				return;
			}

			byte[] bytes = BitConverter.GetBytes(value);
			Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
		}

		public override void Write(decimal value)
		{
			base.Write(value);
			if (InvalidIeee754(value))
			{
				// was written as string
				return;
			}

			int[] intBits = decimal.GetBits(value);
			HasherByteBuffer[0] = (byte)intBits[0];
			HasherByteBuffer[1] = (byte)(intBits[0] >> 8);
			HasherByteBuffer[2] = (byte)(intBits[0] >> 16);
			HasherByteBuffer[3] = (byte)(intBits[0] >> 24);
			HasherByteBuffer[4] = (byte)intBits[1];
			HasherByteBuffer[5] = (byte)(intBits[1] >> 8);
			HasherByteBuffer[6] = (byte)(intBits[1] >> 16);
			HasherByteBuffer[7] = (byte)(intBits[1] >> 24);
			HasherByteBuffer[8] = (byte)intBits[2];
			HasherByteBuffer[9] = (byte)(intBits[2] >> 8);
			HasherByteBuffer[10] = (byte)(intBits[2] >> 16);
			HasherByteBuffer[11] = (byte)(intBits[2] >> 24);
			HasherByteBuffer[12] = (byte)intBits[3];
			HasherByteBuffer[13] = (byte)(intBits[3] >> 8);
			HasherByteBuffer[14] = (byte)(intBits[3] >> 16);
			HasherByteBuffer[15] = (byte)(intBits[3] >> 24);
			Hasher.TransformBlock(HasherByteBuffer, 0, 16, null, 0);
		}

		#endregion Primative Writer Methods

		#region Writer Methods

		protected override void WriteLiteralNull()
		{
			base.WriteLiteralNull();

			if (Hasher != null)
			{
				byte[] bytes = Encoding.UTF8.GetBytes(JsonReader.LiteralNull);
				Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
			}
		}

		#endregion Writer Methods
	}
}