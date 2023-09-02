
# AutoFlats
A simple CLI tool to facilitate the automatic creation of flat frames. It scans directories for light frames, extracts filter, PA and binning from the FITS header, and then creates a list of flat frames to be taken. This can then be used, e.g., in Voyager DragScript to automatically expose the correct flat frames.

---
### Usage

#### 1. `AutoFlats init`: Scans for FITS files and initializes state. Must be called before any of the other commands.
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|<b>`--db <path>`</b> | Path to file where the state is stored. Must not yet exist, or be from a previous session. |
|<b>`--files <path>`</b> | FITS file or directory to include. If this is a directory then it recursively scans for FITS files in the directory. |
|`--rtol <degrees>`          | PA rotation tolerance in degrees. By default rotation is ignored. |
|`--binning`       | Whether binning should be considered. By default binning is ignored. |

#### 2. Repeat until `AutoFlats proceed` returns `END`
##### 2.1`AutoFlats proceed`: Proceeds to the next set of flats to be exposed. Must be called before the filter/rotation/binning commands.
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|<b>`--db <path>`</b> | Path to file where the state is stored. |
##### 2.2`AutoFlats filter`: Returns the filter of the current set of flats to be exposed
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|<b>`--db <path>`</b> | Path to file where the state is stored. |
##### 2.3`AutoFlats rotation`: Returns the PA rotation of the current set of flats to be exposed
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|<b>`--db <path>`</b> | Path to file where the state is stored. |
##### 2.4`AutoFlats binning`: Returns the binning of the current set of flats to be exposed
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|<b>`--db <path>`</b> | Path to file where the state is stored. |
|<b>`--axis <X/Y>`</b> | Binning along X or Y axis. |

#### Optional. `AutoFlats terminate`: Terminates the session early and deletes the state file.
| Argument (bold = required) | Description                   |
|----------------------------|-------------------------------|
|<b>`--db <path>`</b> | Path to file where the state is stored. |
---
