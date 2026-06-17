This folder will allow you to selectively apply the effect only to certain objects.
Feel free to ignore this if you're using the "Screen" output mode of the asset, having it applied all over the screen.

1) Import the appropriate .unitypackage (URP / Builtin).
2) Switch SSCC Output Mode from "Screen" to "_SSCCTexture".
3) Change your materials from "Universal Render Pipeline/Lit" to "Universal Render Pipeline/Lit + Cavity" shader.
4) You will now be able to individually control the strength of the effect per material using the Cavity / Curvature strength material parameters.
Reduce the strength on objects you don't want the effect on, or simply only put "Lit + Cavity" shader only on those objects you want receiving the effect, keeping the rest on the default "Lit". Enjoy!