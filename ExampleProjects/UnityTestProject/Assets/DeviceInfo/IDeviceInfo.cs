namespace Common.DeviceInfo
{
	public interface IDeviceInfo
	{
		string GetOsName();
		string GetOsVersion();
		OsVersion GetNumericOsVersion();
		string GetManufacturer();
		string GetDeviceModelName();
		string GetDeviceName();
		string GetPlatformName();
		long GetSystemUptime();
		string GetClientVersion();
		long GetSystemMemory();
		bool IsPowerSaveMode();
	}
}
