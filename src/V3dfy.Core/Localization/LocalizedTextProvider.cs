namespace V3dfy.Core.Localization;

public delegate string LocalizedTextProvider(
    string key,
    params (string Key, object? Value)[] placeholders);
