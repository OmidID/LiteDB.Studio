using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using Avalonia.Data;
using Avalonia.Data.Core.Plugins;
using Avalonia.Utilities;

namespace LiteDB.Studio;

public class AvaloniaBsonValuePropertyAccessorPlugin : IPropertyAccessorPlugin
{
	public bool Match(object obj, string propertyName) => obj is BsonValue;

	public IPropertyAccessor Start(WeakReference<object> reference, string propertyName)
	{
		_ = reference ?? throw new ArgumentNullException(nameof(reference));
		_ = propertyName ?? throw new ArgumentNullException(nameof(propertyName));

		if (reference.TryGetTarget(out var instance) && instance is BsonValue bsonValue)
			return new Accessor(bsonValue, propertyName);

		return null;
	}

	private class Accessor : PropertyAccessorBase, IWeakSubscriber<PropertyChangedEventArgs>
	{
		private readonly BsonValue _reference;
		private readonly string _key;
		private IDisposable _subscription;
		private bool _eventRaised;
		private bool _sending;

		public Accessor(BsonValue reference, string key)
		{
			_reference = reference ?? throw new ArgumentNullException(nameof(reference));
			_key = key ?? throw new ArgumentNullException(nameof(key));
		}

		public override Type PropertyType => (_reference.IsDocument ? _reference[_key].Type : _reference.Type) switch
		{
			BsonType.MinValue => typeof(BsonValue),
			BsonType.Null => typeof(BsonValue),
			BsonType.Int32 => typeof(int),
			BsonType.Int64 => typeof(long),
			BsonType.Double => typeof(double),
			BsonType.Decimal => typeof(decimal),
			BsonType.String => typeof(string),
			BsonType.Document => typeof(BsonValue),
			BsonType.Array => typeof(Array),
			BsonType.Binary => typeof(byte[]),
			BsonType.ObjectId => typeof(ObjectId),
			BsonType.Guid => typeof(Guid),
			BsonType.Boolean => typeof(bool),
			BsonType.DateTime => typeof(DateTime),
			BsonType.MaxValue => typeof(BsonValue),
			_ => throw new ArgumentOutOfRangeException()
		};
		public override object Value => _reference.IsDocument ? _reference[_key].RawValue : _reference.RawValue;

		public override bool SetValue(object value, BindingPriority priority)
		{
			if (_sending)
				return false;

			_eventRaised = false;
			_reference[_key] = new BsonValue(value);

			if (!_eventRaised)
				SendCurrentValue();

			return true;
		}

		protected override void SubscribeCore()
		{
			SendCurrentValue();
		}

		protected override void UnsubscribeCore()
		{
			_subscription?.Dispose();
			_subscription = null;
		}

		public void OnEvent(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == _key || string.IsNullOrEmpty(e.PropertyName))
			{
				_eventRaised = true;
				SendCurrentValue();
			}
		}

		private void SendCurrentValue()
		{
			try
			{
				_sending = true;
				var value = Value;
				PublishValue(value);
			}
			finally
			{
				_sending = false;
			}
		}
	}
}
