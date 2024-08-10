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

#if !UNITY_5_3_OR_NEWER
using System.Xml;
#endif

#if WINDOWS_STORE
using TP = System.Reflection.TypeInfo;
#else
using TP = System.Type;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using TCU = DLD.JsonFx.TypeCoercionUtility;

namespace DLD.JsonFx
{
	/// <summary>
	/// Writer for producing JSON data
	/// </summary>
	public class JsonWriter : IDisposable
	{
		#region Constants

		public const string JsonMimeType = "application/json";
		public const string JsonFileExtension = ".json";

		internal const string AnonymousTypePrefix = "<>f__AnonymousType";
		const string ErrorMaxDepth = "The maxiumum depth of {0} was exceeded. Check for cycles in object graph.";
		const string ErrorIDictionaryEnumerator = "Types which implement Generic IDictionary<TKey, TValue> must have an IEnumerator which implements IDictionaryEnumerator. ({0})";

		const BindingFlags DefaultBinding = BindingFlags.Default;
		const BindingFlags AllBinding = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		#endregion Constants

		#region Fields

		readonly TextWriter _writer;
		JsonWriterSettings _settings;
		int _depth;
		Dictionary<object, int> _previouslySerializedObjects;

		public ReferenceHandlerWriter ReferenceHandler;

		#endregion Fields

		#region Init

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="output">TextWriter for writing</param>
		public JsonWriter(TextWriter output)
			: this(output, new JsonWriterSettings())
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="output">TextWriter for writing</param>
		/// <param name="settings">JsonWriterSettings</param>
		public JsonWriter(TextWriter output, JsonWriterSettings settings)
		{
			if (output == null)
			{
				throw new ArgumentNullException("output");
			}

			if (settings == null)
			{
				throw new ArgumentNullException("settings");
			}

			_writer = output;
			_settings = settings;
			_writer.NewLine = _settings.NewLine;

			if (settings.HandleCyclicReferences)
			{
				_previouslySerializedObjects = new Dictionary<object, int>();
			}
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="output">Stream for writing</param>
		public JsonWriter(Stream output)
			: this(output, new JsonWriterSettings())
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="output">Stream for writing</param>
		/// <param name="settings">JsonWriterSettings</param>
		public JsonWriter(Stream output, JsonWriterSettings settings)
		{
			if (output == null)
			{
				throw new ArgumentNullException("output");
			}

			if (settings == null)
			{
				throw new ArgumentNullException("settings");
			}

			_writer = new StreamWriter(output, Encoding.UTF8);
			_settings = settings;
			_writer.NewLine = _settings.NewLine;
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="output">file name for writing</param>
		public JsonWriter(string outputFileName)
			: this(outputFileName, new JsonWriterSettings())
		{
		}

		public JsonWriter(JsonWriterSettings settings)
		{
#if WINDOWS_STORE && !DEBUG
			throw new System.NotSupportedException ("Not supported on this platform");
#else
			if (settings == null)
			{
				throw new ArgumentNullException("settings");
			}

			_settings = settings;
#endif
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="output">file name for writing</param>
		/// <param name="settings">JsonWriterSettings</param>
		public JsonWriter(string outputFileName, JsonWriterSettings settings)
		{
#if WINDOWS_STORE && !DEBUG
			throw new System.NotSupportedException ("Not supported on this platform");
#else
			if (outputFileName == null)
			{
				throw new ArgumentNullException("outputFileName");
			}

			if (settings == null)
			{
				throw new ArgumentNullException("settings");
			}

			Stream stream = new FileStream(outputFileName, FileMode.Create, FileAccess.Write, FileShare.Read);
			_writer = new StreamWriter(stream, Encoding.UTF8);
			_settings = settings;
			_writer.NewLine = _settings.NewLine;
#endif
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="output">StringBuilder for appending</param>
		public JsonWriter(StringBuilder output)
			: this(output, new JsonWriterSettings())
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="output">StringBuilder for appending</param>
		/// <param name="settings">JsonWriterSettings</param>
		public JsonWriter(StringBuilder output, JsonWriterSettings settings)
		{
			if (output == null)
			{
				throw new ArgumentNullException("output");
			}

			if (settings == null)
			{
				throw new ArgumentNullException("settings");
			}

			_writer = new StringWriter(output, CultureInfo.InvariantCulture);
			_settings = settings;
			_writer.NewLine = _settings.NewLine;
		}

		#endregion Init

		#region Properties

		/// <summary>
		/// Gets the underlying TextWriter
		/// </summary>
		public TextWriter TextWriter => _writer;

		/// <summary>
		/// Gets and sets the JsonWriterSettings
		/// </summary>
		public JsonWriterSettings Settings
		{
			get => _settings;
			set
			{
				if (value == null)
				{
					value = new JsonWriterSettings();
				}

				_settings = value;
			}
		}

		#endregion Properties

		#region Static Methods

		/// <summary>
		/// A helper method for serializing an object to JSON
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string Serialize(object value)
		{
			StringBuilder output = new StringBuilder();

			using (JsonWriter writer = new JsonWriter(output))
			{
				writer.Write(value);
			}

			return output.ToString();
		}

		#endregion Static Methods

		#region Public Methods

		public void Write(object value)
		{
			Write(value, false);
		}

		protected virtual void Write(object value, bool isProperty, Type fieldType = null)
		{
			if (isProperty)
			{
				WriteSpace();
			}

			if (value == null)
			{
				WriteLiteralNull();
				return;
			}

			var jsonSerializable = value as IJsonSerializable;
			if (jsonSerializable != null)
			{
				try
				{
					if (isProperty)
					{
						_depth++;
						if (_depth > _settings.MaxDepth)
						{
							throw new JsonSerializationException(string.Format(ErrorMaxDepth,
								new object[] { _settings.MaxDepth }));
						}

						WriteLine();
					}

					jsonSerializable.WriteJson(this);
				}
				finally
				{
					if (isProperty)
					{
						_depth--;
					}
				}

				return;
			}

			// must test enumerations before value types
			var valueAsEnum = value as Enum;
			if (valueAsEnum != null)
			{
				Write(valueAsEnum);
				return;
			}

			// Type.GetTypeCode() allows us to more efficiently switch type
			// plus cannot use 'is' for ValueTypes
			var type = value.GetType();

#if WINDOWS_STORE
			if (Type.Equals (type, typeof(bool)))
				{
					this.Write((Boolean)value);
					return;
				}
			else if (Type.Equals (type, typeof(System.Int32)))
			{
				this.Write((Int32)value);
				return;
			}
			else if (Type.Equals (type, typeof(System.Single)))
			{
				this.Write((Single)value);
				return;
			}
			else if (Type.Equals (type, typeof(System.String)))
			{
				this.Write((String)value);
				return;
			}
			else if (Type.Equals (type, typeof(byte)))
				{
					this.Write((Byte)value);
					return;
				}
			else if (Type.Equals (type, typeof(char)))
				{
					this.Write((Char)value);
					return;
				}
			else if (Type.Equals (type, typeof(DateTime)))
				{
					this.Write((DateTime)value);
					return;
				}
			else if (Type.Equals (type, typeof(DBNull)) || Type.Equals (type, null))
				{
					WriteLiteralNull();
					return;
				}
			else if (Type.Equals (type, typeof(Decimal)))
				{
					// From MSDN:
					// Conversions from Char, SByte, Int16, Int32, Int64, Byte, UInt16, UInt32, and UInt64
					// to Decimal are widening conversions that never lose information or throw exceptions.
					// Conversions from Single or Double to Decimal throw an OverflowException
					// if the result of the conversion is not representable as a Decimal.
					this.Write((Decimal)value);
					return;
				}
			else if (Type.Equals (type, typeof(double)))
				{
					this.Write((Double)value);
					return;
				}
			else if (Type.Equals (type, typeof(System.Int16)))
				{
					this.Write((Int16)value);
					return;
				}
			else if (Type.Equals (type, typeof(System.Int64)))
				{
					this.Write((Int64)value);
					return;
				}
			else if (Type.Equals (type, typeof(System.SByte)))
				{
					this.Write((SByte)value);
					return;
				}
			else if (Type.Equals (type, typeof(System.UInt16)))
				{
					this.Write((UInt16)value);
					return;
				}
			else if (Type.Equals (type, typeof(System.UInt32)))
				{
					this.Write((UInt32)value);
					return;
				}
			else if (Type.Equals (type, typeof(System.UInt64)))
				{
					this.Write((UInt64)value);
					return;
				}
			else
				{
					// all others must be explicitly tested
			}
#else
			// Faster to switch on typecode, but Windows Store does not support it
			switch (TP.GetTypeCode(type))
			{
				case TypeCode.Boolean:
				{
					Write((bool)value);
					return;
				}
				case TypeCode.Byte:
				{
					Write((byte)value);
					return;
				}
				case TypeCode.Char:
				{
					Write((char)value);
					return;
				}
				case TypeCode.DateTime:
				{
					Write((DateTime)value);
					return;
				}
				case TypeCode.DBNull:
				case TypeCode.Empty:
				{
					WriteLiteralNull();
					return;
				}
				case TypeCode.Decimal:
				{
					// From MSDN:
					// Conversions from Char, SByte, Int16, Int32, Int64, Byte, UInt16, UInt32, and UInt64
					// to Decimal are widening conversions that never lose information or throw exceptions.
					// Conversions from Single or Double to Decimal throw an OverflowException
					// if the result of the conversion is not representable as a Decimal.
					Write((decimal)value);
					return;
				}
				case TypeCode.Double:
				{
					Write((double)value);
					return;
				}
				case TypeCode.Int16:
				{
					Write((short)value);
					return;
				}
				case TypeCode.Int32:
				{
					Write((int)value);
					return;
				}
				case TypeCode.Int64:
				{
					Write((long)value);
					return;
				}
				case TypeCode.SByte:
				{
					Write((sbyte)value);
					return;
				}
				case TypeCode.Single:
				{
					Write((float)value);
					return;
				}
				case TypeCode.String:
				{
					Write((string)value);
					return;
				}
				case TypeCode.UInt16:
				{
					Write((ushort)value);
					return;
				}
				case TypeCode.UInt32:
				{
					Write((uint)value);
					return;
				}
				case TypeCode.UInt64:
				{
					Write((ulong)value);
					return;
				}
				default:
				case TypeCode.Object:
				{
					// all others must be explicitly tested
					break;
				}
			}
#endif

			var converter = Settings.GetConverter(type);
			if (converter != null && (_depth != 0 || converter.ConvertAtDepthZero))
			{
				converter.Write(this, type, value);
				return;
			}

			if (value is Guid)
			{
				Write((Guid)value);
				return;
			}

			var valueAsUri = value as Uri;
			if (valueAsUri != null)
			{
				Write(valueAsUri);
				return;
			}

			if (value is TimeSpan)
			{
				Write((TimeSpan)value);
				return;
			}

			var valueAsVersion = value as Version;
			if (valueAsVersion != null)
			{
				Write(valueAsVersion);
				return;
			}

			// IDictionary test must happen BEFORE IEnumerable test
			// since IDictionary implements IEnumerable
			var valueAsDictionary = value as IDictionary;
			if (valueAsDictionary != null)
			{
				try
				{
					if (isProperty)
					{
						_depth++;
						if (_depth > _settings.MaxDepth)
						{
							throw new JsonSerializationException(string.Format(ErrorMaxDepth,
								new object[] { _settings.MaxDepth }));
						}

						WriteLine();
					}

					WriteObject(valueAsDictionary);
				}
				finally
				{
					if (isProperty)
					{
						_depth--;
					}
				}

				return;
			}

			//if (!Type.Equals (TCU.GetTypeInfo(type).GetInterface (JsonReader.TypeGenericIDictionary), null))
			if (TypeCoercionUtility.GetTypeInfo(typeof(IDictionary))
			    .IsAssignableFrom(TypeCoercionUtility.GetTypeInfo(value.GetType())))
			{
				try
				{
					if (isProperty)
					{
						_depth++;
						if (_depth > _settings.MaxDepth)
						{
							throw new JsonSerializationException(string.Format(ErrorMaxDepth,
								new object[] { _settings.MaxDepth }));
						}

						WriteLine();
					}

					WriteDictionary((IEnumerable)value);
				}
				finally
				{
					if (isProperty)
					{
						_depth--;
					}
				}

				return;
			}

			var list = value as IList;
			if (list != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
			{
				// Is List<T>
				var memberType = type.GetGenericArguments()[0];

				try
				{
					if (isProperty)
					{
						_depth++;
						if (_depth > _settings.MaxDepth)
						{
							throw new JsonSerializationException(string.Format(ErrorMaxDepth,
								new object[] { _settings.MaxDepth }));
						}

						WriteLine();
					}

					WriteArray(list, memberType);
				}
				finally
				{
					if (isProperty)
					{
						_depth--;
					}
				}

				return;
			}

			// IDictionary test must happen BEFORE IEnumerable test
			// since IDictionary implements IEnumerable
			var enumerable = value as IEnumerable;
			if (enumerable != null)
			{
#if !UNITY_5_3_OR_NEWER
				var xmlNode = enumerable as XmlNode;
				if (xmlNode != null)
				{
					Write(xmlNode);
					return;
				}
#endif
				try
				{
					if (isProperty)
					{
						_depth++;
						if (_depth > _settings.MaxDepth)
						{
							throw new JsonSerializationException(string.Format(ErrorMaxDepth,
								new object[] { _settings.MaxDepth }));
						}

						WriteLine();
					}

					WriteArray(enumerable, type.GetElementType());
				}
				finally
				{
					if (isProperty)
					{
						_depth--;
					}
				}

				return;
			}

			// structs and classes
			try
			{
				if (isProperty)
				{
					_depth++;
					if (_depth > _settings.MaxDepth)
					{
						throw new JsonSerializationException(string.Format(ErrorMaxDepth,
							new object[] { _settings.MaxDepth }));
					}

					WriteLine();
				}

				WriteObject(value, type, true, fieldType);
			}
			finally
			{
				if (isProperty)
				{
					_depth--;
				}
			}
		}

		public virtual void WriteBase64(byte[] value)
		{
			Write(Convert.ToBase64String(value));
		}

		public virtual void WriteHexString(byte[] value)
		{
			if (value == null || value.Length == 0)
			{
				Write(string.Empty);
				return;
			}

			StringBuilder builder = new StringBuilder();

			// Loop through each byte of the binary data
			// and format each one as a hexadecimal string
			for (int i = 0; i < value.Length; i++)
			{
				builder.Append(value[i].ToString("x2"));
			}

			// the hexadecimal string
			Write(builder.ToString());
		}

		public virtual void Write(DateTime value)
		{
			if (_settings.DateTimeSerializer != null)
			{
				_settings.DateTimeSerializer(this, value);
				return;
			}

			switch (value.Kind)
			{
				case DateTimeKind.Local:
				{
					value = value.ToUniversalTime();
					goto case DateTimeKind.Utc;
				}
				case DateTimeKind.Utc:
				{
					// UTC DateTime in ISO-8601
					Write(string.Format("{0:s}Z", new object[] { value }));
					break;
				}
				default:
				{
					// DateTime in ISO-8601
					Write(string.Format("{0:s}", new object[] { value }));
					break;
				}
			}
		}

		public virtual void Write(Guid value)
		{
			Write(value.ToString("D"));
		}

		public virtual void Write(Enum value)
		{
			string enumName = null;

			var type = value.GetType();

			if (type.IsDefined(typeof(FlagsAttribute), true) && !Enum.IsDefined(type, value))
			{
				var flags = GetFlagList(type, value);
				var flagNames = new string[flags.Length];
				for (int i = 0; i < flags.Length; i++)
				{
					flagNames[i] = JsonNameAttribute.GetJsonName(flags[i], _settings.SerializedName);
					if (string.IsNullOrEmpty(flagNames[i]))
					{
						flagNames[i] = flags[i].ToString("f");
					}
				}

				enumName = string.Join(", ", flagNames);
			}
			else
			{
				enumName = JsonNameAttribute.GetJsonName(value, _settings.SerializedName);
				if (string.IsNullOrEmpty(enumName))
				{
					enumName = value.ToString("f");
				}
			}

			Write(enumName);
		}

		public virtual void Write(string value)
		{
			if (value == null)
			{
				WriteLiteralNull();
				return;
			}

			int start = 0,
				length = value.Length;

			_writer.Write(JsonReader.OperatorStringDelim);

			for (int i = start; i < length; i++)
			{
				char ch = value[i];

				if (ch <= '\u001F' ||
				    ch >= '\u007F' ||
				    ch == '<' || // improves compatibility within script blocks
				    ch == JsonReader.OperatorStringDelim ||
				    ch == JsonReader.OperatorCharEscape)
				{
					if (i > start)
					{
						_writer.Write(value.Substring(start, i - start));
					}

					start = i + 1;

					switch (ch)
					{
						case JsonReader.OperatorStringDelim:
						case JsonReader.OperatorCharEscape:
						{
							_writer.Write(JsonReader.OperatorCharEscape);
							_writer.Write(ch);
							continue;
						}
						case '\b':
						{
							_writer.Write("\\b");
							continue;
						}
						case '\f':
						{
							_writer.Write("\\f");
							continue;
						}
						case '\n':
						{
							_writer.Write("\\n");
							continue;
						}
						case '\r':
						{
							_writer.Write("\\r");
							continue;
						}
						case '\t':
						{
							_writer.Write("\\t");
							continue;
						}
						default:
						{
							_writer.Write("\\u");
							_writer.Write(char.ConvertToUtf32(value, i).ToString("X4"));
							continue;
						}
					}
				}
			}

			if (length > start)
			{
				_writer.Write(value.Substring(start, length - start));
			}

			_writer.Write(JsonReader.OperatorStringDelim);
		}

		#endregion Public Methods

		#region Primative Writer Methods

		public virtual void Write(bool value)
		{
			_writer.Write(value ? JsonReader.LiteralTrue : JsonReader.LiteralFalse);
		}

		public virtual void Write(byte value)
		{
			_writer.Write(value.ToString("g", CultureInfo.InvariantCulture));
		}

		public virtual void Write(sbyte value)
		{
			_writer.Write(value.ToString("g", CultureInfo.InvariantCulture));
		}

		public virtual void Write(short value)
		{
			_writer.Write(value.ToString("g", CultureInfo.InvariantCulture));
		}

		public virtual void Write(ushort value)
		{
			_writer.Write(value.ToString("g", CultureInfo.InvariantCulture));
		}

		public virtual void Write(int value)
		{
			_writer.Write(value.ToString("g", CultureInfo.InvariantCulture));
		}

		public virtual void Write(uint value)
		{
			if (InvalidIeee754(value))
			{
				// emit as string since Number cannot represent
				Write(value.ToString("g", CultureInfo.InvariantCulture));
				return;
			}

			_writer.Write(value.ToString("g", CultureInfo.InvariantCulture));
		}

		public virtual void Write(long value)
		{
			if (InvalidIeee754(value))
			{
				// emit as string since Number cannot represent
				Write(value.ToString("g", CultureInfo.InvariantCulture));
				return;
			}

			_writer.Write(value.ToString("g", CultureInfo.InvariantCulture));
		}

		public virtual void Write(ulong value)
		{
			if (InvalidIeee754(value))
			{
				// emit as string since Number cannot represent
				Write(value.ToString("g", CultureInfo.InvariantCulture));
				return;
			}

			_writer.Write(value.ToString("g", CultureInfo.InvariantCulture));
		}

		public virtual void Write(float value)
		{
			if (float.IsNaN(value) || float.IsInfinity(value))
			{
				WriteLiteralNull();
			}
			else
			{
				_writer.Write(value.ToString("r", CultureInfo.InvariantCulture));
			}
		}

		public virtual void Write(double value)
		{
			if (double.IsNaN(value) || double.IsInfinity(value))
			{
				WriteLiteralNull();
			}
			else
			{
				_writer.Write(value.ToString("r", CultureInfo.InvariantCulture));
			}
		}

		public virtual void Write(decimal value)
		{
			if (InvalidIeee754(value))
			{
				// emit as string since Number cannot represent
				Write(value.ToString("g", CultureInfo.InvariantCulture));
				return;
			}

			_writer.Write(value.ToString("g", CultureInfo.InvariantCulture));
		}

		public virtual void Write(char value)
		{
			Write(new string(value, 1));
		}

		public virtual void Write(TimeSpan value)
		{
			Write(value.Ticks);
		}

		public virtual void Write(Uri value)
		{
			Write(value.ToString());
		}

		public virtual void Write(Version value)
		{
			Write(value.ToString());
		}

#if !UNITY_5_3_OR_NEWER
		public virtual void Write(XmlNode value)
		{
			// TODO: auto-translate XML to JsonML
			Write(value.OuterXml);
		}
#endif

		#endregion Primative Writer Methods

		#region Writer Methods

		protected internal virtual void WriteArray(IEnumerable value, Type elementType)
		{
			var appendDelim = false;

			WriteOperatorArrayStart();

			// Are the references to the elements of this array tracked
			bool handledRef = ReferenceHandler != null &&
			                  !Equals(elementType, null) &&
			                  ReferenceHandler.IsHandled(elementType);

			_depth++;
			if (_depth > _settings.MaxDepth)
			{
				throw new JsonSerializationException(string.Format(ErrorMaxDepth, new object[] { _settings.MaxDepth }));
			}

			try
			{
				foreach (object item in value)
				{
					if (appendDelim)
					{
						WriteArrayItemDelim();
					}
					else
					{
						appendDelim = true;
					}

					WriteLine();

					if (!handledRef || item == null)
					{
						// Reference is not tracked, serialize it normally
						WriteArrayItem(item, elementType);
					}
					else
					{
						// Reference is not owned by this field, write a reference to it
						// Arrays/Lists cannot own references
						WriteArrayItem("@" + ReferenceHandler.GetReferenceID(item), typeof(string));
					}
				}
			}
			finally
			{
				_depth--;
			}

			if (appendDelim)
			{
				WriteLine();
			}

			WriteOperatorArrayEnd();
		}

		protected virtual void WriteArrayItem(object item, Type elementType)
		{
			Write(item, false, elementType);
		}

		protected virtual void WriteObject(IDictionary value)
		{
			WriteDictionary(value);
		}

		protected virtual void WriteDictionary(IEnumerable value)
		{
			IDictionaryEnumerator enumerator = value.GetEnumerator() as IDictionaryEnumerator;
			if (enumerator == null)
			{
				throw new JsonSerializationException(string.Format(ErrorIDictionaryEnumerator,
					new object[] { value.GetType() }));
			}

			bool appendDelim = false;

			if (_settings.HandleCyclicReferences)
			{
				if (_previouslySerializedObjects.TryGetValue(value, out int prevIndex))
				{
					WriteOperatorObjectStart();
					WriteObjectProperty("@ref", prevIndex);
					WriteLine();
					WriteOperatorObjectEnd();
					return;
				}

				_previouslySerializedObjects.Add(value, _previouslySerializedObjects.Count);
			}

			WriteOperatorObjectStart();

			_depth++;
			if (_depth > _settings.MaxDepth)
			{
				throw new JsonSerializationException(string.Format(ErrorMaxDepth, new object[] { _settings.MaxDepth }));
			}

			try
			{
				while (enumerator.MoveNext())
				{
					if (appendDelim)
					{
						WriteObjectPropertyDelim();
					}
					else
					{
						appendDelim = true;
					}

					bool handledRef = ReferenceHandler != null &&
					                  enumerator.Entry.Value != null &&
					                  ReferenceHandler.IsHandled(enumerator.Entry.Value.GetType());

					if (!handledRef)
					{
						// Reference is not tracked, serialize it normally
						WriteObjectProperty(Convert.ToString(enumerator.Entry.Key), enumerator.Entry.Value);
					}
					else
					{
						// Reference is not owned by this field, write a reference to it
						// Dictionaries cannot own references
						WriteObjectProperty(Convert.ToString(enumerator.Entry.Key),
							"@" + ReferenceHandler.GetReferenceID(enumerator.Entry.Value), typeof(string));
					}
				}
			}
			finally
			{
				_depth--;
			}

			if (appendDelim)
			{
				WriteLine();
			}

			WriteOperatorObjectEnd();
		}

		void WriteObjectProperty(string key, object value, Type fieldType = null)
		{
			WriteLine();
			WriteObjectPropertyName(key);
			WriteOperatorNameDelim();
			WriteObjectPropertyValue(value, fieldType);
		}

		protected virtual void WriteObjectPropertyName(string name)
		{
			Write(name);
		}

		protected virtual void WriteObjectPropertyValue(object value, Type fieldType = null)
		{
			Write(value, true, fieldType);
		}

		protected virtual void WriteObject(object value, Type type, bool serializePrivate, Type fieldType = null)
		{
			bool appendDelim = false;

			if (_settings.HandleCyclicReferences && !TypeCoercionUtility.GetTypeInfo(type).IsValueType)
			{
				if (_previouslySerializedObjects.TryGetValue(value, out int prevIndex))
				{
					WriteOperatorObjectStart();
					WriteObjectProperty("@ref", prevIndex);
					WriteLine();
					WriteOperatorObjectEnd();
					return;
				}

				_previouslySerializedObjects.Add(value, _previouslySerializedObjects.Count);
			}

			WriteOperatorObjectStart();

			_depth++;
			if (_depth > _settings.MaxDepth)
			{
				throw new JsonSerializationException(string.Format(ErrorMaxDepth, new object[] { _settings.MaxDepth }));
			}

			try
			{
				if (!string.IsNullOrEmpty(_settings.TypeHintName) &&
				    (!_settings.TypeHintsOnlyWhenNeeded || _depth <= 1 || (fieldType != type && !Equals(fieldType, null))))
				{
					if (appendDelim)
						WriteObjectPropertyDelim();
					else
						appendDelim = true;

					WriteObjectProperty(_settings.TypeHintName, type.FullName + ", " + type.Assembly.GetName().Name);
				}


				// Set tag so that other objects can find it when deserializing
				// Note that this must be set after the type hint is serialized
				// To make sure the fields are read correctly when deserializing

				if (ReferenceHandler != null && ReferenceHandler.IsHandled(type))
				{
					if (appendDelim)
						WriteObjectPropertyDelim();
					else
						appendDelim = true;

					WriteObjectProperty("@tag", ReferenceHandler.GetReferenceID(value));

					// Notify the reference handler that this value has now been serialized
					// Will throw an exception if it has already been serialized
					ReferenceHandler.MarkAsSerialized(value);
				}

				//Console.WriteLine ("Anon " + anonymousType);

				//Console.WriteLine (type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Length  + " " + type.GetProperties().Length);

				// serialize public properties

				KeyValuePair<string, FieldInfo>[] fields;
				KeyValuePair<string, PropertyInfo>[] properties;
				_settings.Coercion.GetMemberWritingMap(type, _settings, out fields, out properties);

				for (int j = 0; j < properties.Length; j++)
				{
					PropertyInfo property = properties[j].Value;

					object propertyValue = property.GetValue(value, null);
					if (IsDefaultValue(property, propertyValue))
					{
						if (Settings.DebugMode)
							Console.WriteLine("Cannot serialize " + property.Name + " : is default value");
						continue;
					}


					if (appendDelim)
						WriteObjectPropertyDelim();
					else
						appendDelim = true;

					string name = properties[j].Key;

					bool ownedRef = ReferenceHandler == null ||
					                !ReferenceHandler.IsHandled(property.PropertyType) ||
					                ReferenceHandler.IsOwnedRef(property);

					if (ownedRef || propertyValue == null)
					{
						// Reference is owned by this property, serialize it as normal
						WriteObjectProperty(name, propertyValue, property.PropertyType);
					}
					else
					{
						// Reference is not owned by this property, write a reference to it
						WriteObjectProperty(name, "@" + ReferenceHandler.GetReferenceID(propertyValue), typeof(string));
					}
				}

				for (int j = 0; j < fields.Length; j++)
				{
					FieldInfo field = fields[j].Value;

					object fieldValue = field.GetValue(value);
					if (IsDefaultValue(field, fieldValue))
					{
						if (Settings.DebugMode)
							Console.WriteLine("Cannot serialize " + field.Name + " : is default value");
						continue;
					}

					if (appendDelim)
						WriteObjectPropertyDelim();
					else
						appendDelim = true;

					string name = fields[j].Key;

					bool ownedRef = ReferenceHandler == null ||
					                !ReferenceHandler.IsHandled(field.FieldType) ||
					                ReferenceHandler.IsOwnedRef(field);

					if (ownedRef || fieldValue == null)
					{
						// Reference is owned by this field, serialize it as normal
						WriteObjectProperty(name, fieldValue, field.FieldType);
					}
					else
					{
						// Reference is not owned by this field, write a reference to it
						WriteObjectProperty(name, "@" + ReferenceHandler.GetReferenceID(fieldValue), typeof(string));
					}
				}
			}
			finally
			{
				_depth--;
			}

			if (appendDelim)
				WriteLine();

			WriteOperatorObjectEnd();
		}

		protected virtual void WriteLiteralNull()
		{
			_writer.Write(JsonReader.LiteralNull);
		}

		protected virtual void WriteOperatorArrayStart()
		{
			_writer.Write(JsonReader.OperatorArrayStart);
		}

		protected virtual void WriteOperatorArrayEnd()
		{
			_writer.Write(JsonReader.OperatorArrayEnd);
		}

		protected virtual void WriteOperatorObjectStart()
		{
			_writer.Write(JsonReader.OperatorObjectStart);
		}

		protected virtual void WriteOperatorObjectEnd()
		{
			_writer.Write(JsonReader.OperatorObjectEnd);
		}

		protected virtual void WriteOperatorNameDelim()
		{
			_writer.Write(JsonReader.OperatorNameDelim);
		}

		protected virtual void WriteArrayItemDelim()
		{
			_writer.Write(JsonReader.OperatorValueDelim);
		}

		protected virtual void WriteObjectPropertyDelim()
		{
			_writer.Write(JsonReader.OperatorValueDelim);
		}

		protected virtual void WriteLine()
		{
			if (!_settings.PrettyPrint)
			{
				return;
			}

			_writer.WriteLine();
			for (int i = 0; i < _depth; i++)
			{
				_writer.Write(_settings.Tab);
			}
		}

		protected virtual void WriteSpace()
		{
			if (!_settings.PrettyPrint)
			{
				return;
			}

			_writer.Write(' ');
		}

		#endregion Writer Methods

		#region Private Methods

		/// <summary>
		/// Determines if the member value matches the DefaultValue attribute
		/// </summary>
		/// <returns>if has a value equivalent to the DefaultValueAttribute</returns>
		bool IsDefaultValue(MemberInfo member, object value)
		{
#if JSON_FX_USE_DEFAULT_VALUE_ATTRIBUTE
			#if WINDOWS_STORE
			var attribute = member.GetCustomAttribute<DefaultValueAttribute> (true);
			#else
			var attribute = Attribute.GetCustomAttribute(member, typeof(DefaultValueAttribute)) as DefaultValueAttribute;
			#endif

			if (attribute == null)
			{
				return false;
			}

			if (attribute.Value == null)
			{
				return (value == null);
			}

			return (attribute.Value.Equals(value));
#else
			return false;
#endif
		}

		#endregion Private Methods

		#region Utility Methods

		/// <summary>
		/// Splits a bitwise-OR'd set of enums into a list.
		/// </summary>
		/// <param name="enumType">the enum type</param>
		/// <param name="value">the combined value</param>
		/// <returns>list of flag enums</returns>
		/// <remarks>
		/// from PseudoCode.EnumHelper
		/// </remarks>
		static Enum[] GetFlagList(Type enumType, object value)
		{
			ulong longVal = Convert.ToUInt64(value);
			Array enumValues = Enum.GetValues(enumType);

			List<Enum> enums = new List<Enum>(enumValues.Length);

			// check for empty
			if (longVal == 0L)
			{
				// Return the value of empty, or zero if none exists
				enums.Add((Enum)Convert.ChangeType(value, enumType));
				return enums.ToArray();
			}

			for (int i = enumValues.Length - 1; i >= 0; i--)
			{
				ulong enumValue = Convert.ToUInt64(enumValues.GetValue(new[] { i }));

				if ((i == 0) && (enumValue == 0L))
				{
					continue;
				}

				// matches a value in enumeration
				if ((longVal & enumValue) == enumValue)
				{
					// remove from val
					longVal -= enumValue;

					// add enum to list
					enums.Add(enumValues.GetValue(new[] { i }) as Enum);
				}
			}

			if (longVal != 0x0L)
			{
				enums.Add(Enum.ToObject(enumType, value) as Enum);
			}

			return enums.ToArray();
		}

		/// <summary>
		/// Determines if a numberic value cannot be represented as IEEE-754.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		protected virtual bool InvalidIeee754(decimal value)
		{
			// http://stackoverflow.com/questions/1601646

			try
			{
				return (decimal)((double)value) != value;
			}
			catch
			{
				return true;
			}
		}

		#endregion Utility Methods

		#region IDisposable Members

		void IDisposable.Dispose()
		{
			if (_writer != null)
			{
				_writer.Dispose();
			}
		}

		#endregion IDisposable Members
	}
}