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
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DLD.JsonFx
{
	/// <summary>
	/// Goes through the exact same data that gets serialized by
	/// a <see cref="JsonWriter"/>, but doesn't write it to a stream.
	/// Instead it only puts that data through a
	/// <see cref="System.Security.Cryptography.HashAlgorithm"/>
	/// to output a hash code at the end.
	/// </summary>
	/// <seealso cref="JsonWriterWithHasher"/>
	public class HashGenerator : JsonWriter
	{
		#region Fields

		// from https://stackoverflow.com/a/3621316
		protected readonly HashAlgorithm Hasher;
		protected readonly byte[] HasherByteBuffer = new byte[16];

		#endregion Fields

		#region Init

		public HashGenerator(JsonWriterSettings settings, string hashAlgorithm) : base(settings)
		{
			Hasher = HashAlgorithm.Create(hashAlgorithm);
			Hasher?.Initialize();
		}

		#endregion Init

		#region Public Methods

		public override void Write(string value)
		{
			if (value == null)
			{
				WriteLiteralNull();
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
			HasherByteBuffer[0] = value ? (byte)1 : (byte)0;
			Hasher.TransformBlock(HasherByteBuffer, 0, 1, null, 0);
		}

		public override void Write(byte value)
		{
			HasherByteBuffer[0] = value;
			Hasher.TransformBlock(HasherByteBuffer, 0, 1, null, 0);
		}

		public override void Write(sbyte value)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
		}

		public override void Write(short value)
		{
			unchecked
			{
				HasherByteBuffer[0] = (byte)(value >> 8);
				HasherByteBuffer[1] = (byte)value;

				Hasher.TransformBlock(HasherByteBuffer, 0, 2, null, 0);
			}
		}

		public override void Write(ushort value)
		{
			unchecked
			{
				HasherByteBuffer[0] = (byte)(value >> 8);
				HasherByteBuffer[1] = (byte)value;

				Hasher.TransformBlock(HasherByteBuffer, 0, 2, null, 0);
			}
		}

		public override void Write(int value)
		{
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
			if (InvalidIeee754(value))
			{
				// emit as string since Number cannot represent
				Write(value.ToString("g", CultureInfo.InvariantCulture));
				return;
			}

			byte[] bytes = BitConverter.GetBytes(value);
			Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
		}

		public override void Write(long value)
		{
			if (InvalidIeee754(value))
			{
				// emit as string since Number cannot represent
				Write(value.ToString("g", CultureInfo.InvariantCulture));
				return;
			}

			byte[] bytes = BitConverter.GetBytes(value);
			Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
		}

		public override void Write(ulong value)
		{
			if (InvalidIeee754(value))
			{
				// emit as string since Number cannot represent
				Write(value.ToString("g", CultureInfo.InvariantCulture));
				return;
			}

			byte[] bytes = BitConverter.GetBytes(value);
			Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
		}

		public override void Write(float value)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
		}

		public override void Write(double value)
		{
			byte[] bytes = BitConverter.GetBytes(value);
			Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
		}

		public override void Write(decimal value)
		{
			if (InvalidIeee754(value))
			{
				// emit as string since Number cannot represent
				Write(value.ToString("g", CultureInfo.InvariantCulture));
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
			byte[] bytes = Encoding.UTF8.GetBytes(JsonReader.LiteralNull);
			Hasher.TransformBlock(bytes, 0, bytes.Length, null, 0);
		}

		protected override void WriteOperatorArrayStart()
		{
		}

		protected override void WriteOperatorArrayEnd()
		{
		}

		protected override void WriteOperatorObjectStart()
		{
		}

		protected override void WriteOperatorObjectEnd()
		{
		}

		protected override void WriteOperatorNameDelim()
		{
		}

		protected override void WriteArrayItemDelim()
		{
		}

		protected override void WriteObjectPropertyDelim()
		{
		}

		protected override void WriteLine()
		{
		}

		protected override void WriteSpace()
		{
		}

		#endregion Writer Methods
	}
}