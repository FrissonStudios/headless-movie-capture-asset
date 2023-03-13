-= Headless Movie Capture v2.0.0 =-

Headless Movie Capture is a simple to use component that just needs to be attached to your main Camera. With as few options as possible, it captures the render output into a MP4 h264 encoded file, GIF or can be streamed.
We now support sound (with aac codec) and streaming to local and external services, and we bundle a few presets for the most common streaming services.

NORMAL USAGE:
- Select your main camera and add the component 'HeadlessMovieCapture'
- Select the framerate that will be encoded on the output .mp4 file.
- Check the 'Flip Result', if the mp4 output is upside down.
- Select the 'Encoding preset': 'Very Fast' gives large files but has a very low CPU overhead, while 'Very Slow' gives smaller files at the expense of more CPU usage, which can cause frame drops.
- You can drop a 'Playable Director' or Timeline object in the 'Timeline' option. With this, capture will only happen during the timeline range.
- Set the 'Output'. Relative paths have a default root on the project or player folder. Don't give an extension, this will be added. Tokens $camera and $date are supported with custom date format.
- 'Open Output Folder' will cause the folder where the file is saved to be open after the recording.
- Press 'CAPTURE' or play in the editor to start capturing. On a build, this should be controlled via a script that enable/disables the 'HeadlessMovieCapture' component.

Any error on the component setup is shown on the top of the component.
In case of 0 bytes files, please enable file logging and check the HeadlessStudioMoviveCapture.log in the root of the project or runtime package. It should show any error that occur.

ADVANCED USAGE:
It's possible to replace the bundle ffmpeg by your own version. This allows to use other codecs, but we don't support those versions, use at your own risk.
We also only enable by default 3 codecs, since those are the ones that give the least overhead when recording. It's possible to enable others, but the performance may not be optimal.
If you want to force a given framerate, realtime option can be disable. This will force unity to run with Time.captureFramerate set to the choosen framerate, and should work fine for pre-recorded camera animations, or other uses that don't require realtime feedback.
There is also a sample script showing how to control the start and stop of recording.

PRESETS:
You can define your own presets, besides the ones we bundle. They are defined in the presets.json on the Resource folder. The presets is an editor only feature, and changing the preset field at runtime as no effect, presets are applied to the component settings and are saved with the scene.


C# code is not encrypted, so it's easy to adjust to fit any project needs.
