Respository containing useful assets for using ROS.NET inside Unity

Either clone the repository or copy it's contents directly into your PROJECT/Assets directory.

Please see the tutorial scene, as well as the tutorial Publisher/Subscriber to quickly get started.

Dependencies
=============
Unity Standard Assets


Communicating with ROS

To get started, Add the PREFAB [ROSMASTER] to your scene,and copy the file ROS.txt from Assets/ROS.Net_Unity_Assets to your Assets directory.
Modify the fields for ROS_MASTER_URI, ROS_HOSTNAME, and NODENAME accordingly. You may rename this file as you wish, but set this file as the
textasset to the [ROSMASTER] gameobject. 


You should now be able to communicate with ROS


LoadMesh
To use the loadmesh, place your robot models inside the Resources/UnityResources folder.
