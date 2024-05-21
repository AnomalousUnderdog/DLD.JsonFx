using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace DLD.JsonFx
{
	public class JsonOwnedReferenceAttribute : Attribute
	{
	}

	public class JsonHandledReferenceAttribute : Attribute
	{
	}

	public class ReferenceHandlerWriter
	{
		Dictionary<object, int> _mapping = new Dictionary<object, int>();
		Dictionary<object, bool> _hasBeenSerialized = new Dictionary<object, bool>();
		Dictionary<Type, bool> _isHandledCache = new Dictionary<Type, bool>();
		Dictionary<MemberInfo, bool> _isOwnedRefCache = new Dictionary<MemberInfo, bool>();

		public int GetReferenceID(object val)
		{
			int id;
			if (!_mapping.TryGetValue(val, out id))
			{
				id = _mapping.Count + 1;
				_mapping.Add(val, id);
				_hasBeenSerialized[val] = false;
			}

			return id;
		}

		public bool IsHandled(Type type)
		{
			bool result;
			if (!_isHandledCache.TryGetValue(type, out result))
			{
				result = _isHandledCache[type] =
					type.GetCustomAttributes(typeof(JsonHandledReferenceAttribute), true).Length != 0;
			}

			return result;
		}

		public bool IsOwnedRef(MemberInfo info)
		{
			bool result;
			if (!_isOwnedRefCache.TryGetValue(info, out result))
			{
				result = _isOwnedRefCache[info] =
					info.GetCustomAttributes(typeof(JsonOwnedReferenceAttribute), true).Length != 0;
			}

			return result;
		}

		/// <summary>
		/// Marks this value as having been serialized somewhere with the correct field set.
		/// After serialization we can then know what references were not serialized anywhere, which helps with debugging
		/// </summary>
		/// <param name="val"></param>
		public void MarkAsSerialized(object val)
		{
			_hasBeenSerialized[val] = true;
		}

		public List<object> GetNonSerializedReferences()
		{
			var ls = new List<object>();
			foreach (var pair in _hasBeenSerialized)
			{
				if (!pair.Value)
				{
					ls.Add(pair.Key);
				}
			}

			return ls;
		}

		public void TransferNonSerializedReferencesToReader(ReferenceHandlerReader reader)
		{
			foreach (var pair in _hasBeenSerialized)
			{
				if (!pair.Value)
				{
					reader.Set(_mapping[pair.Key], pair.Key);
				}
			}
		}
	}

	public class ReferenceHandlerReader
	{
		Dictionary<int, object> _mapping = new Dictionary<int, object>();

		Dictionary<int, List<KeyValuePair<MemberInfo, object>>> _delayedSetters =
			new Dictionary<int, List<KeyValuePair<MemberInfo, object>>>();

		Dictionary<int, List<KeyValuePair<string, IDictionary>>> _delayedDictSetters =
			new Dictionary<int, List<KeyValuePair<string, IDictionary>>>();

		Dictionary<int, List<KeyValuePair<int, IList>>> _delayedListSetters =
			new Dictionary<int, List<KeyValuePair<int, IList>>>();

		public bool TryGetValueFromID(int id, out object result)
		{
			return _mapping.TryGetValue(id, out result);
		}

		public void AddDelayedSetter(int id, MemberInfo memberInfo, object val)
		{
			if (val == null)
			{
				throw new ArgumentNullException("val");
			}

			if (!_delayedSetters.ContainsKey(id))
			{
				_delayedSetters[id] = new List<KeyValuePair<MemberInfo, object>>();
			}

			//System.Console.WriteLine ("Adding delayed setter for " + id);
			_delayedSetters[id].Add(new KeyValuePair<MemberInfo, object>(memberInfo, val));
		}

		public void AddDelayedDictionarySetter(int id, IDictionary dict, string key)
		{
			if (key == null)
			{
				throw new ArgumentNullException("key");
			}

			if (dict == null)
			{
				throw new ArgumentNullException("dict");
			}

			if (!_delayedDictSetters.ContainsKey(id))
			{
				_delayedDictSetters[id] = new List<KeyValuePair<string, IDictionary>>();
			}

			//System.Console.WriteLine ("Adding delayed setter for " + id);
			_delayedDictSetters[id].Add(new KeyValuePair<string, IDictionary>(key, dict));
		}

		public void AddDelayedListSetter(int id, IList list, int index)
		{
			if (list == null)
			{
				throw new ArgumentNullException("list");
			}

			if (!_delayedListSetters.ContainsKey(id))
			{
				_delayedListSetters[id] = new List<KeyValuePair<int, IList>>();
			}

			//System.Console.WriteLine ("Adding delayed setter for " + id);
			_delayedListSetters[id].Add(new KeyValuePair<int, IList>(index, list));
		}

		public void Set(int id, object val)
		{
			_mapping.Add(id, val);

			if (_delayedSetters.ContainsKey(id))
			{
				var setters = _delayedSetters[id];

				for (int i = 0; i < setters.Count; i++)
				{
					var memberInfo = setters[i].Key;
					var target = setters[i].Value;

					if (memberInfo is PropertyInfo)
					{
						// set value of public property
						((PropertyInfo)memberInfo).SetValue(target, val, null);
					}
					else if (memberInfo is FieldInfo)
					{
						// set value of public field
						((FieldInfo)memberInfo).SetValue(target, val);
					}
				}

				setters.Clear();
				_delayedSetters.Remove(id);
			}

			if (_delayedDictSetters.ContainsKey(id))
			{
				var setters = _delayedDictSetters[id];

				for (int i = 0; i < setters.Count; i++)
				{
					var key = setters[i].Key;
					var target = setters[i].Value;

					target[key] = val;
				}

				setters.Clear();
				_delayedDictSetters.Remove(id);
			}

			if (_delayedListSetters.ContainsKey(id))
			{
				var setters = _delayedListSetters[id];

				for (int i = 0; i < setters.Count; i++)
				{
					var index = setters[i].Key;
					var target = setters[i].Value;

					target[index] = val;
				}

				setters.Clear();
				_delayedListSetters.Remove(id);
			}
		}

		public List<int> GetMissingReferences()
		{
			var dict = new Dictionary<int, bool>();

			foreach (var pair in _delayedSetters)
			{
				dict[pair.Key] = true;
			}

			foreach (var pair in _delayedDictSetters)
			{
				dict[pair.Key] = true;
			}

			foreach (var pair in _delayedListSetters)
			{
				dict[pair.Key] = true;
			}

			var ls = new List<int>();
			foreach (var pair in dict)
			{
				ls.Add(pair.Key);
			}

			return ls;
		}
	}
}