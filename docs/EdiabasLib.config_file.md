# Properties of the EdiabasLib.config file
The `EdiabasLib.config` file is a replacement for the standard `EDIABAS.INI` file. It must be located in the same directory as the `EdiabasLib.dll` or `Api32.dll` file.
The following properties could be specified in this file:

## Standard properties
* `Interface`: Interface to use: _STD:OBD_, _ADS_ (ADS operates also with FTDI USB converter) or _ENET_.
* `ApiTrace`: API debug level (0..1)
* `IfhTrace`: Interface debug level (0..3)
* `TraceBuffering`: 1=Enable buffering of API trace files
* `EcuPath`: Path to the ecu files
* `RetryComm`: 1=Retry communication
* `EnetRemoteHost`: Remote host for ENET protocol. Possible values are:
	* `ip address`: No broadcast, directly connect to specified host.
	* `auto`: Broadcast to 169.254.255.255, Identical to EDIABAS `RemoteHost = Autodetect`.
	* `auto:all`: Broadcast to all network interfaces.
	* `auto:<interface name>`: Broadcast to all interfaces that start with `<interface name>` (case ignored).
* `EnetTesterAddress`: Tester address for ENET protocol, standard is 0xF4
* `EnetControlPort`: Control port for ENET protocol, standard is 6811
* `EnetDiagnosticPort`: Diagnostic port for ENET protocol, standard is 6801
* `EnetTimeoutConnect`: Connect timeout for ENET protocol, default is 5000

## Non standard properties
* `ObdComPort`: COM port name for OBD interface.
* `AdsComPort`: COM port name for ADS interface (if not specifed ObdComPort will be used).
* `AppendTrace`: 0=Override old log file, 1=Always append the logfiles.
* `LockTrace`: 0=Allow changing `IfhTrace` level from the application, 1=Prevent changing the `IfhTrace` level from the application.

## FTDI D2XX driver properties
For improved timing (especially required for ADS adapter) it's possible to access the FTDI D2XX driver directly. Android also supports access to FTDI USB D-CAN/K-Line adapters. To activate this mode use FTDI instead of COM in the com port name for _ObdComPort_ or _AdsComPort_. There are multiply ways the select the USB device:
* `FTDIx`: select device by index x (starting with 0)
* `FTDI:SER=[serial number](serial-number)`: select device by serial number
* `FTDI:DESC=[description](description)`: select device by description (not supported for Android)
* `FTDI:LOC=[location](location)`: select device by location id (either decimal or hex with 0x) (not supported for Android)
To get the device location information you could use FT Prog from the FTDI web site.

## Bluetooth (Android)
Since Android devices normally have no COM ports, it's possible to connect via Bluetooth using the Bluetooth Serial Port Protocol (SPP).
To select a specific Bluetooth device use `BLUETOOTH:<device address>`.  
When using ELM327 adapters append `;ELM327` after the Bluetooth address to specify ELM327 mode.
For adapters that simply send data without modification append `;RAW`.

## Bluetooth (PC)
It's possible to use the [Replacement firmware for ELM327](Replacement_firmware_for_ELM327.md) also with a PC. When connecting the adapter with the PC two serial COM ports are created (incoming and outgoing).
To use the adapter specify `STD:OBD` for the `interface` and `BLUETOOTH:<outgoing COM port>` or `BLUETOOTH:<device address>` for `ObdComPort`.  
The recommended way for setting up the configuration is to use the [EdiabasLibConfigTool](Replacement_firmware_for_ELM327.md#use-the-adapter-with-inpa-tool32-or-ista-d).

## WiFi
For WiFi based ELM327 adapters you have to specify `STD:OBD` for the `interface` and `ELM327WIFI` for `ObdComPort`.  
When using the [Replacement firmware for ELM327](Replacement_firmware_for_ELM327.md) with WiFi devices specify `DEEPOBDWIFI` for `ObdComPort`.
