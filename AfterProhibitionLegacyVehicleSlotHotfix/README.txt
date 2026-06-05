After Prohibition Legacy Vehicle Slot Hotfix
============================================

Purpose
-------

This tiny compatibility DLL is for the archived Days Of Prohibition v1.3.98 package.
It leaves the legacy GameplayTweaks.dll untouched and only adds missing vanilla vehicle
template aliases to GameplayTweaks' multi-crew slot lookup at runtime.

Current aliases:

- vehicle-town-car = 4
- vehicle-car-preorder = 4
- vehicle-bulletproof-car = 4
- vehicle-bulletproof-jaguar = 2

Expected log:

[Info   :After Prohibition Legacy Vehicle Slot Hotfix] vehicle-slot-hotfix applied source=awake added=... updated=...

