# Wifi-AutoConnect
Xamarin Android App for Android TV box to ensure the Wi-Fi is connected at all times.

The MXQ-Pro Android box I have drops its Wi-Fi all the time and it is annoying to go and connect the Wi-Fi all the time.

This Xamarin app runs a background service permanently (boot, restart, app-start) and listens to Wi-Fi changes. If there are no Wi-Fi changes, the alarm which runs every 60 seconds will detect no connection in anycase and connect it for you.

The background service runs every 10 seconds to ensure it is running and the Wi-Fi is connected.

## Todo
- Change txtNetworkName to a dropdown list, listing all currently available SSIDs
