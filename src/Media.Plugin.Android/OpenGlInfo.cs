using Android.Opengl;
using Plugin.CurrentActivity;
using System;
namespace Plugin.Media
{
    public static class OpenGlInfo
    {
		public static int MaxTextureSize()
		{
			int[] maxTextureSize = new int[1];
			CrossCurrentActivity.Current.Activity.RunOnUiThread(() => {
				EGLDisplay dpy = null;
				EGLSurface surf = null;
				EGLContext ctx = null;
				try
				{
					dpy = EGL14.EglGetDisplay(EGL14.EglDefaultDisplay);
					int[] vers = new int[2];
					EGL14.EglInitialize(dpy, vers, 0, vers, 1);
					int[] configAttrs = {
						EGL14.EglColorBufferType, EGL14.EglRgbBuffer,
						EGL14.EglLevel, 0,
						EGL14.EglRenderableType, EGL14.EglOpenglEs2Bit,
						EGL14.EglSurfaceType, EGL14.EglPbufferBit,
						EGL14.EglNone
					};
					var configs = new EGLConfig[1];
					var numConfig = new int[1];
					EGL14.EglChooseConfig(dpy, configAttrs, 0, configs, 0, 1, numConfig, 0);
					if (numConfig[0] == 0) {
						throw new Exception("No GL config");
					}
					var config = configs[0];
					var surfAttr = new int[] {
						EGL14.EglWidth, 64, EGL14.EglHeight, 64, EGL14.EglNone
					};
					surf = EGL14.EglCreatePbufferSurface(dpy, config, surfAttr, 0);
					var ctxAttrib = new int[] {
						EGL14.EglContextClientVersion, 2, EGL14.EglNone
					};
					ctx = EGL14.EglCreateContext(dpy, config, EGL14.EglNoContext, ctxAttrib, 0);
					EGL14.EglMakeCurrent(dpy, surf, surf, ctx);
					GLES20.GlGetIntegerv(GLES20.GlMaxTextureSize, maxTextureSize, 0);
				}
				finally
				{
					if (dpy != null)
					{
						EGL14.EglMakeCurrent(dpy, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext);
						if (ctx != null)
						{
							EGL14.EglDestroyContext(dpy, ctx);
						}
						if (surf != null)
						{
							EGL14.EglDestroySurface(dpy, surf);
						}
						EGL14.EglTerminate(dpy);
					}
				}
			});
			return maxTextureSize[0];
		}
    }
}
