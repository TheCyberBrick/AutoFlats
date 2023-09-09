
# AutoFlats
A simple CLI tool to facilitate the automatic creation of flat frames. It scans directories for light frames, extracts filter, PA and binning from the FITS header, and then creates a list of flat frames to be taken. This can then be used, e.g., in Voyager DragScript to automatically expose the correct flat frames.

---
### Usage

#### 1. `AutoFlats init`: Scans for FITS files and initializes state. Must be called before any of the other commands.
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|`--db <path>` | Path to file where the state is stored. Must not yet exist, or be from a previous session. |
|<b>`--files <path>`</b> | FITS file or directory to include. If this is a directory then it recursively scans for FITS files in the directory. |
|`--rtol <degrees>`          | PA rotation tolerance in degrees. By default rotation is ignored. |
|`--binning`       | Whether binning should be considered. By default binning is ignored. |

#### 2. Repeat until `AutoFlats proceed` returns `END`
##### 2.1 `AutoFlats proceed`: Proceeds to the next set of flats to be exposed. Must be called before the filter/rotation/binning/stack commands. Returns `END` if all necessary flats have been created.
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|`--db <path>` | Path to file where the state is stored. |
##### 2.2 `AutoFlats filter`: Returns the filter of the current set of flats to be exposed
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|`--db <path>` | Path to file where the state is stored. |
##### 2.3 `AutoFlats rotation`: Returns the PA rotation of the current set of flats to be exposed
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|`--db <path>` | Path to file where the state is stored. |
##### 2.4 `AutoFlats binning`: Returns the binning of the current set of flats to be exposed
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|`--db <path>` | Path to file where the state is stored. |
|<b>`--axis <X/Y>`</b> | Binning along X or Y axis. |
##### 2.5 Expose flats for the given filter, rotation and binning
##### 2.6 Optional. `AutoFlats stack`: Integrates the current set of flats into a single stacked and calibrated master flat
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|`--db <path>` | Path to file where the state is stored. |
|`--method <method>` | Stacking method/program to be used for stacking and calibrating the flat frames. Currently `siril` is the only valid value. |
|`--applicationpath <path>` | Path to command line application to be used for stacking. For example, if Siril is used as stacking method then this must point to siril-cli.exe (e.g. \"C:\\Program Files\\Siril\\bin\\siril-cli.exe\"). Default value points to the default install location of Siril. |
|<b>`--flats <path>`</b> | Path to flat frames. Must be a directory containing all flats. The flats may be located in subdirectories. |
|`--darks <path>` | Path to dark(s) used for calibrating the flat frames. Can be a directory or FITS file. Calibration is skipped if none specified. |
|`--exptol <seconds>` | Exposure time tolerance in seconds. Used for matching darks to flats during calibration. Default: 5 seconds. |
|`--keeponlymasterflat` | If set, only the master flat is kept and the other flat frames are deleted. |
|`--outputprefix` | Prefix added to the output file. |
|`--outputsuffix` | Suffix added to the output file. |
|`--skipifmissingdarks` | If set, stacking is skipped if darks are missing instead of aborting with an error. |
##### 2.7 Optional. `AutoFlats masterFlat`: Returns the path of the stacked master flat of the current set of flats
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|`--db <path>` | Path to file where the state is stored. |
##### 2.8 Optional. `AutoFlats calibrate`: Calibrates all lights matching the current set of flats with a stacked master flat
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|`--db <path>` | Path to file where the state is stored. |
|`--method <method>` | Stacking method/program to be used for calibrating the light frames. |
|`--applicationpath <path>` | Path to command line application to be used for calibration. For example, if Siril is used as calibration method then this must point to siril-cli.exe (e.g. \"C:\\Program Files\\Siril\\bin\\siril-cli.exe\"). Default value points to the default install location of Siril. |
|<b>`--lights <path>`</b> | Path to light frames. Must be a directory containing all lights. The lights may be located in subdirectories. |
|<b>`--darks <path>`</b> | Path to dark(s) used for calibrating the light frames. Can be a directory or FITS file. |
|`--exptol <seconds>` | Exposure time tolerance in seconds. Used for matching darks to lights during calibration. Default: 5 seconds. |
|`--keeponlycalibratedlights` | If set, only the calibrated lights are kept and the other light frames are deleted. |
|`--outputprefix` | Prefix added to the output file(s). |
|`--outputsuffix` | Suffix added to the output file(s). |
|`--skipifmissingdarks` | If set, calibration is skipped if darks are missing instead of aborting with an error. |
|`--skipifmissingflats` | If set, calibration is skipped if master flat is missing instead of aborting with an error. |

#### Optional. `AutoFlats terminate`: Terminates the session early and deletes the state file.
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|`--db <path>` | Path to file where the state is stored. |
---
