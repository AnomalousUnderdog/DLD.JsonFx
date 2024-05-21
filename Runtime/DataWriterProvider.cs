#region License

/*---------------------------------------------------------------------------------*\

	Distributed under the terms of an MIT-style license:

	The MIT License

	Copyright (c) 2006-2009 Stephen M. McKamey

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

#if WINDOWS_STORE
using TP = System.Reflection.TypeInfo;
#else
using TP = System.Type;
#endif
using System;
using System.Collections.Generic;
using System.IO;

namespace DLD.JsonFx
{
	public interface IDataWriterProvider
	{
		IDataWriter DefaultDataWriter { get; }

		IDataWriter Find(string extension);

		IDataWriter Find(string acceptHeader, string contentTypeHeader);
	}

	/// <summary>
	/// Provides lookup capabilities for finding an IDataWriter
	/// </summary>
	public class DataWriterProvider : IDataWriterProvider
	{
		#region Fields

		readonly IDataWriter _defaultWriter;

		readonly IDictionary<string, IDataWriter> _writersByExt =
			new Dictionary<string, IDataWriter>(StringComparer.OrdinalIgnoreCase);

		readonly IDictionary<string, IDataWriter> _writersByMime =
			new Dictionary<string, IDataWriter>(StringComparer.OrdinalIgnoreCase);

		#endregion Fields

		#region Init

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="writers">inject with all possible writers</param>
		public DataWriterProvider(IEnumerable<IDataWriter> writers)
		{
			if (writers != null)
			{
				foreach (IDataWriter writer in writers)
				{
					if (_defaultWriter == null)
					{
						// TODO: decide less arbitrary way to choose default
						// without hardcoding value into IDataWriter
						_defaultWriter = writer;
					}

					if (!string.IsNullOrEmpty(writer.ContentType))
					{
						_writersByMime[writer.ContentType] = writer;
					}

					if (!string.IsNullOrEmpty(writer.ContentType))
					{
						string ext = NormalizeExtension(writer.FileExtension);
						_writersByExt[ext] = writer;
					}
				}
			}
		}

		#endregion Init

		#region Properties

		public IDataWriter DefaultDataWriter => _defaultWriter;

		#endregion Properties

		#region Methods

		public IDataWriter Find(string extension)
		{
			extension = NormalizeExtension(extension);

			if (_writersByExt.ContainsKey(extension))
			{
				return _writersByExt[extension];
			}

			return null;
		}

		public IDataWriter Find(string acceptHeader, string contentTypeHeader)
		{
			foreach (string type in ParseHeaders(acceptHeader, contentTypeHeader))
			{
				if (_writersByMime.ContainsKey(type))
				{
					return _writersByMime[type];
				}
			}

			return null;
		}

		#endregion Methods

		#region Utility Methods

		/// <summary>
		/// Parses HTTP headers for Media-Types
		/// </summary>
		/// <param name="accept">HTTP Accept header</param>
		/// <param name="contentType">HTTP Content-Type header</param>
		/// <returns>sequence of Media-Types</returns>
		/// <remarks>
		/// http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html
		/// </remarks>
		public static IEnumerable<string> ParseHeaders(string accept, string contentType)
		{
			string mime;

			// check for a matching accept type
			foreach (string type in SplitTrim(accept, ','))
			{
				mime = ParseMediaType(type);
				if (!string.IsNullOrEmpty(mime))
				{
					yield return mime;
				}
			}

			// fallback on content-type
			mime = ParseMediaType(contentType);
			if (!string.IsNullOrEmpty(mime))
			{
				yield return mime;
			}
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static string ParseMediaType(string type)
		{
			foreach (string mime in SplitTrim(type, ';'))
			{
				// only return first part
				return mime;
			}

			// if no parts then was empty
			return string.Empty;
		}

		static IEnumerable<string> SplitTrim(string source, char ch)
		{
			if (string.IsNullOrEmpty(source))
			{
				yield break;
			}

			int length = source.Length;
			for (int prev = 0, next = 0; prev < length && next >= 0; prev = next + 1)
			{
				next = source.IndexOf(ch, prev);
				if (next < 0)
				{
					next = length;
				}

				string part = source.Substring(prev, next - prev).Trim();
				if (part.Length > 0)
				{
					yield return part;
				}
			}
		}

		static string NormalizeExtension(string extension)
		{
			if (string.IsNullOrEmpty(extension))
			{
				return string.Empty;
			}

			// ensure is only extension with leading dot
			return Path.GetExtension(extension);
		}

		#endregion Utility Methods
	}
}