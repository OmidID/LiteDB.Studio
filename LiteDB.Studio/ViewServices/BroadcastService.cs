using System;

namespace LiteDB.Studio.ViewServices;

public class BroadcastService
{
	public static BroadcastService Instance { get; } = new BroadcastService();

	private static class Holder<T>
	{
		internal static event Action<T> NotificationRegistration;

		internal static void OnNotificationRegistration(T e)
		{
			NotificationRegistration?.Invoke(e);
		}
	}

	public void Register<T>(Action<T> callback)
	{
		Holder<T>.NotificationRegistration += callback;
	}

	public void Broadcast<T>(T value)
	{
		Holder<T>.OnNotificationRegistration(value);
	}
}
