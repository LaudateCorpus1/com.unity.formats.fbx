# FbxExporters

Copyright (c) 2017 Unity Technologies. All rights reserved.

Licensed under the ##LICENSENAME##.
See LICENSE.md file for full license information.

**Version**: 0.0.5a

Requirements
------------

* [FBX SDK C# Bindings v0.0.4a or higher](https://github.com/Unity-Technologies/FbxSharp)

Command-line Installing Maya2017 Integration
--------------------------------------------

You can install the package and integrations from the command-line using the following script:

MacOS:

export UNITY3D_PATH=/Applications/Unity\ 2017.1.0f3/Unity.app/Contents/MacOS/Unity

export PROJECT_PATH=~/Development/FbxExporters
export PACKAGE_NAME=FbxExporters
export PACKAGE_VERSION={CurrentVersion}
export FBXEXPORTERS_PACKAGE_PATH=${PROJECT_PATH}/${PACKAGE_NAME}_${PACKAGE_VERSION}.unitypackage

# must be non-batch to import correctly
"${UNITY3D_PATH}" -projectPath "${PROJECT_PATH}" -importPackage ${FBXEXPORTERS_PACKAGE_PATH} -quit

# Use "InstallMaya2017CommandsOnly" to install without UI
"${UNITY3D_PATH}" -batchMode -projectPath "${PROJECT_PATH}" -executeMethod FbxExporters.Integrations.InstallMaya2017 -quit

Configuring Auto-loading of plugin in Maya2017
----------------------------------------------

MacOS:

export MAYA_PATH=/Applications/Autodesk/maya2017/Maya.app/Contents/bin/maya

"${MAYA_PATH}" -command "loadPlugin unityOneClickPlugin; pluginInfo -edit -autoload true unityOneClickPlugin;quit;"

