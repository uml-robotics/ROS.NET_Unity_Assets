//Gregory Lemasurier
//University of Massachusuettes Lowell
using Ros_CSharp;
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using ti = Messages.theora_image_transport;
using IntPtr = System.IntPtr;
using ogg_packet_ptr = System.IntPtr;
using th_setup_info_ptr = System.IntPtr;
using th_dec_ctx_ptr = System.IntPtr;
using System.Threading;

/* To use this you must compile the dlls for libogg and libtheora. https://www.theora.org/downloads/
 * Then you must copy the two dlls to the Assets/Plugins folder */

/// <summary>
/// A subscriber to a ROS Theora stream that outputs the image to an attached object's texture
/// </summary>
public class TheoraSubscriber : MonoBehaviour
{
    /// <summary>
    /// The ROSCore for the subscriber
    /// </summary>
    [Tooltip("The roscore to use, if null then will try to find one.")]
    public ROSCore roscore;

    /// <summary>
    /// The ROS topic to subscribe to
    /// </summary>
    [Tooltip("The topic to subscribe to")]
    public string Topic;

    /// <summary>
    /// The GameObject to add the texture to
    /// </summary>
    [Tooltip("The object to add a texture to")]
    public GameObject Obj;

    /*
     * Taken from libogg.h
     */
    /// <summary>
    /// Encapsulates the data and metadata belonging to a single raw Ogg/Vorbis packet
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Ogg_Packet
    {
        public byte[] packet;
        public int bytes;
        public int b_o_s;
        public int e_o_s;

        public long granulepos;

        public long packetno;
    };

    /*
     * Taken from libtheora's codec.h
     */
    /// <summary>
    /// Contains Theora bitstream information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Th_Info
    {
        /// <summary>
        /// Bitstream version information
        /// </summary>
        byte version_major;
        /// <summary>
        /// Bitstream version information
        /// </summary>
        byte version_minor;
        /// <summary>
        /// Bitstream version information
        /// </summary>
        byte version_subminor;
        /// <summary>
        /// The encoded frame width. This must be a multiple of 16, and less than 1048576.
        /// </summary>
        UInt32 frame_width;
        /// <summary>
        /// The encoded frame height. This must be a multiple of 16, and less than 1048576.
        /// </summary>
        UInt32 frame_height;
        /// <summary>
        /// The displayed picture width. This must be no larger than width.
        /// </summary>
        UInt32 pic_width;
        /// <summary>
        /// The displayed picture height. This must be no larger than height.
        /// </summary>
        UInt32 pic_height;
        /// <summary>
        /// The X offset of the displayed picture. This must be no larger than 
        /// #frame_width-#pic_width or 255, whichever is smaller.
        /// </summary>
        UInt32 pic_x;
        /// <summary>
        /// The Y offset of the displayed picture. This must be no larger than #frame_height-#pic_height,
        /// and #frame_height-#pic_height-#pic_y must be no larger than 255. This slightly funny restriction
        /// is due to the fact that the offset is specified from the top of the image for consistency with
        /// the standard graphics left-handed coordinate system used throughout this API, while it is stored
        /// in the encoded stream as an offset from the bottom.
        /// </summary>
        UInt32 pic_y;
        /// <summary>
        /// The numerator of the frame rate, as a fraction. If 0, the frame rate is undefined
        /// </summary>
        UInt32 fps_numerator;
        /// <summary>
        /// The denominator of the frame rate, as a fraction. If 0, the frame rate is undefined
        /// </summary>
        UInt32 fps_denominator;
        /// <summary>
        /// The numerator of the aspect ratio of the pixels. If the value is zero, the aspect ratio is undefined.
        /// If not specified by any external means, 1:1 should be assumed.The aspect ratio of the full picture can
        /// be computed as: aspect_numerator*pic_width/(aspect_denominator* pic_height).
        /// </summary>
        UInt32 aspect_numerator;
        /// <summary>
        /// The denominator of the aspect ratio of the pixels. If the value is zero, the aspect ratio is undefined.
        /// If not specified by any external means, 1:1 should be assumed.The aspect ratio of the full picture can
        /// be computed as: aspect_numerator*pic_width/(aspect_denominator* pic_height).
        /// </summary>
        UInt32 aspect_denominator;
        /// <summary>
        /// The Colorspace
        /// </summary>
        Th_Colorspace colorspace;
        /// <summary>
        /// The Pixel format
        /// </summary>
        public Th_Pixel_Fmt pixel_fmt;
        /// <summary>
        /// The target bit-rate in bits per second. If initializing an encoder with this struct, set this field
        /// to a non-zero value to activate CBR encoding by default.
        /// </summary>
        int target_bitrate;
        /// <summary>
        /// The target quality level. Valid values range from 0 to 63, inclusive, with higher values giving
        /// higher quality. If initializing an encoder with this struct, and #target_bitrate is set to zero,
        /// VBR encoding at this quality will be activated by default.
        /// Currently this is set so that a qi of 0 corresponds to distortions of 24 times the JND, and each
        /// increase by 16 halves that value. This gives us fine discrimination at low qualities, yet effective
        /// rate control at high qualities. The qi value 63 is special, however. For this, the highest quality,
        /// we use one half of a JND for our threshold. Due to the lower bounds placed on allowable quantizers
        /// in Theora, we will not actually be able to achieve quality this good, but this should provide as 
        /// close to visually lossless quality as Theora is capable of. We could lift the quantizer restrictions
        /// without breaking VP3.1 compatibility, but this would result in quantized coefficients that are too 
        /// large for the current bitstream to be able to store. We'd have to redesign the token syntax to store
        /// these large coefficients, which would make transcoding complex.
        /// </summary>
        int quality;
        /// <summary>
        /// The amount to shift to extract the last keyframe number from the granule position.
        /// This can be at most 31. th_info_init() will set this to a default value(currently 6,
        /// which is good for streaming applications), but you can set it to 0 to make every
        /// frame a keyframe. The maximum distance between key frames is 1 #keyframe_granule_shift.
        /// The keyframe frequency can be more finely controlled with 
        /// #TH_ENCCTL_SET_KEYFRAME_FREQUENCY_FORCE, which can also be adjusted during encoding
        /// (for example, to force the next frame to be a keyframe), but it cannot be set larger
        /// than the amount permitted by this field after the headers have been output.
        /// </summary>
        int keyframe_granule_shift;
    };

    /*
     * Taken from libtheora's codec.h
     */
    /// <summary>
    /// Contains Theora comment information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Th_Comment
    {
        /// <summary>
        /// The array of comment string vectors.
        /// </summary>
        IntPtr user_comments;
        /// <summary>
        /// An array of the corresponding length of each vector, in bytes.
        /// </summary>
        IntPtr comment_lengths;
        /// <summary>
        /// The total number of comment strings.
        /// </summary>
        int comments;
        /// <summary>
        /// The null-terminated vendor string. This identifies the software used to encode the stream.
        /// </summary>
        IntPtr vendor;
    };

    /*
     * Taken from libtheora's codec.h
     */
    /// <summary>
    /// A buffer for a single color plane in an uncompressed image
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Th_Img_Plane
    {
        /// <summary>
        /// The width of this plane.
        /// </summary>
        public int width;
        /// <summary>
        /// The height of this plane.
        /// </summary>
        public int height;
        /// <summary>
        /// The offset in bytes between successive rows.
        /// </summary>
        public int stride;
        /// <summary>
        /// A pointer to the beginning of the first row.
        /// </summary>
        public IntPtr data;
    };

    /*
     * Taken from libtheora's codec.h
     */
    /// <summary>
    /// The currently defined color space tags
    /// </summary>
    private enum Th_Colorspace
    {
        /// <summary>
        /// The color space was not specified at the encoder. It may be conveyed by an external means.
        /// </summary>
        TH_CS_UNSPECIFIED,
        /// <summary>
        /// A color space designed for NTSC content.
        /// </summary>
        TH_CS_ITU_REC_470M,
        /// <summary>
        /// A color space designed for PAL/SECAM content.
        /// </summary>
        TH_CS_ITU_REC_470BG,
        /// <summary>
        /// The total number of currently defined color spaces.
        /// </summary>
        TH_CS_NSPACES
    };

    /*
     * Taken from libtheora's codec.h
     */
    /// <summary>
    /// The currently defined pixel format tags
    /// </summary>
    private enum Th_Pixel_Fmt
    {
        /// <summary>
        /// Chroma decimation by 2 in both the X and Y directions (4:2:0). The Cb and Cr
        /// chroma planes are half the width and half the height of the luma plane.
        /// </summary>
        TH_PF_420,
        /// <summary>
        /// Currently reserved.
        /// </summary>
        TH_PF_RSVD,
        /// <summary>
        /// Chroma decimation by 2 in the X direction (4:2:2). The Cb and Cr chroma planes
        /// are half the width of the luma plane, but full height.
        /// </summary>
        TH_PF_422,
        /// <summary>
        /// No chroma decimation (4:4:4). The Cb and Cr chroma planes are full width and full height.
        /// </summary>
        TH_PF_444,
        /// <summary>
        /// The total number of currently defined pixel formats.
        /// </summary>
        TH_PF_NFORMATS
    };

    /*
     * Taken from libtheora's codec.h
     */
    /// <summary>
    /// Return codes for Theora decode
    /// </summary>
    private enum Ret_Values
    {
        /// <summary>
        /// An invalid pointer was provided.
        /// </summary>
        TH_EFAULT = -1,
        /// <summary>
        /// An invalid argument was provided.
        /// </summary>
        TH_EINVAL = -10,
        /// <summary>
        /// The contents of the header were incomplete, invalid, or unexpected.
        /// </summary>
        TH_EBADHEADER = -20,
        /// <summary>
        /// The header does not belong to a Theora stream.
        /// </summary>
        TH_ENOTFORMAT = -21,
        /// <summary>
        /// The bitstream version is too high.
        /// </summary>
        TH_EVERSION = -22,
        /// <summary>
        /// The specified function is not implemented.
        /// </summary>
        TH_EIMPL = -23,
        /// <summary>
        /// There were errors in the video data packet.
        /// </summary>
        TH_EBADPACKET = -24,
        /// <summary>
        /// The decoded packet represented a dropped frame. The player can continue to
        /// display the current frame, as the contents of thedecoded frame buffer have
        /// not changed.
        /// </summary>
        TH_DUPFRAME = 1,
    };

    /*
     * Function definition taken from libtheora's codec.h. Calls libtheora's source.
     */
    /// <summary>
    /// Initializes a Th_Info structure. This should be called on a freshly allocated Th_Info structure before attempting to use it.
    /// </summary>
    /// <param name="c">The Th_Info struct to initialize.</param>
    [DllImport("libtheora", CallingConvention = CallingConvention.Cdecl, EntryPoint = "th_info_init")]
    private static extern void th_info_init(ref Th_Info c);

    /*
     * Function definition taken from libtheora's codec.h. Calls libtheora's source.
     */
    /// <summary>
    /// Clears a Th_Info structure. This should be called on a Th_Info structure after it is no longer needed.
    /// </summary>
    /// <param name="_info">The Th_Info struct to clear.</param>
    [DllImport("libtheora", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, EntryPoint = "th_info_clear")]
    private static extern void th_info_clear(ref Th_Info _info);

    /*
     * Function definition taken from libtheora's codec.h. Calls libtheora's source.
     */
    /// <summary>
    /// Initialize a Th_Comment structure. This should be called on a freshly allocated Th_Comment structure before attempting to use it.
    /// </summary>
    /// <param name="tc">The Th_Comment struct to initialize.</param>
    [DllImport("libtheora", CallingConvention = CallingConvention.Cdecl, EntryPoint = "th_comment_init")]
    private static extern void th_comment_init(ref Th_Comment tc);

    /*
     * Function definition taken from libtheora's codec.h. Calls libtheora's source.
     */
    /// <summary>
    /// Clears a Th_Comment structure. This should be called on a Th_Comment structure after it is no longer needed. It will free all memory used by the structure members.
    /// </summary>
    /// <param name="_tc">The Th_Comment struct to clear.</param>
    [DllImport("libtheora", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, EntryPoint = "th_comment_clear")]
    private static extern void th_comment_clear(ref Th_Comment _tc);

    /*
     * Function definition taken from libtheora's theoradec.h. Calls libtheora's source.
     */
    /// <summary>
    /// Decodes the header packets of a Theora stream. 
    /// This should be called on the initial packets of the stream, in succession, until it returns 0, 
    /// indicating that all headers have been processed, or an error is encountered. At least three header packets are required, and additional optional header
    /// packets may follow. This can be used on the first packet of any logical stream to determine if that stream is a Theora stream.
    /// </summary>
    /// <param name="_info">A Th_Info structure to fill in. This must have been previously initialized with th_info_init(). 
    /// The application may immediately begin using the contents of this structure after the first header is decoded, though it
    /// must continue to be passed in on all subsequent calls.</param>
    /// <param name="_tc">A Th_Comment structure to fill in. The application may immediately begin using the contents of 
    /// this structure after the second header is decoded, though it must continue to be passed in on all subsequent calls.</param>
    /// <param name="_setup">Returns a pointer to additional, private setup information needed by the decoder.
    /// The contents of this pointer must be initialized to IntPtr.Zero on the first call, and the returned value must
    /// continue to be passed in on all subsequent calls.</param>
    /// <param name="_op">An Ogg_Packet structure which contains one of the initial packets of an Ogg logical stream.</param>
    /// <returns> 
    /// A positive value indicates that a Theora header was successfully processed.
    /// 0, The first video data packet was encountered after all required header packets were parsed. 
    ///     The packet just passed in on this call should be savedand fed to th_decode_packetin() to begin decodingvideo data.
    /// TH_EFAULT, One of _info, _tc, or _setup was IntPtr.Zero
    /// TH_EBADHEADER, _op was IntPtr.Zero, the packet was not the next header packet in the expected sequence, or the format of
    ///     the header data was invalid.
    /// TH_EVERSION, The packet data was a Theora info header, but for a bitstream version not decodable with this version of libtheoradec.
    /// TH_ENOTFORMAT, The packet was not a Theora header.
    /// </returns>
    [DllImport("libtheora", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, EntryPoint = "th_decode_headerin")]
    private static extern int th_decode_headerin(ref Th_Info _info, ref Th_Comment _tc, ref th_setup_info_ptr _setup, ogg_packet_ptr _op);

    /*
     * Function definition taken from libtheora's theoradec.h. Calls libtheora's source.
     */
    /// <summary>
    /// Allocates a decoder instance.
    /// Security Warning: The Theora format supports very large frame sizes, potentially even larger than the address space of a 32-bit machine,
    /// and creating a decoder context allocates the space for several frames of data. If the allocation fails here, your program will crash,
    /// possibly at some future point because the OS kernel returned a valid memory range and will only fail when it tries to map the pages in it
    /// the first time they are used. Even if it succeeds, you may experience a denial of service if the frame size is large enough to cause 
    /// excessive paging.If you are integrating libtheora in a larger application where such things are undesirable, it is highly recommended that
    /// you check the frame size in _info before calling this function and refuse to decode streams where it is larger than some reasonable maximum.
    /// libtheora will not check this for you, because there may be machines that can handle such streams and applications that wish to.
    /// </summary>
    /// <param name="_info">A Th_Info struct filled via th_decode_headerin().</param>
    /// <param name="_setup">A Th_Setup_info handle returned via th_decode_headerin().</param>
    /// <returns>The initialized th_dec_ctx handle or NULL If the decoding parameters were invalid.</returns>
    [DllImport("libtheora", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, EntryPoint = "th_decode_alloc")]
    private static extern th_dec_ctx_ptr th_decode_alloc(ref Th_Info _info, th_setup_info_ptr _setup);

    /*
     * Function definition taken from libtheora's theoradec.h. Calls libtheora's source.
     */
    /// <summary>
    /// Releases all storage used for the decoder setup information. This should be called after you no longer want to create any decoders 
    /// for a stream whose headers you have parsed with th_decode_headerin().
    /// </summary>
    /// <param name="_setup">The setup information to free. This can safely be IntPtr.Zero.</param>
    [DllImport("libtheora", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, EntryPoint = "th_setup_free")]
    private static extern void th_setup_free(th_setup_info_ptr _setup);

    /*
     * Function definition taken from libtheora's theoradec.h. Calls libtheora's source.
     */
    /// <summary>
    /// Submits a packet containing encoded video data to the decoder.
    /// </summary>
    /// <param name="_dec">A th_dec_ctx handle.</param>
    /// <param name="_op">An Ogg_Packet containing encoded video data.</param>
    /// <param name="_granpos">Returns the granule position of the decoded packet. If non-<tt>NULL</tt>, the granule position for 
    /// this specific packet is stored in this location. This is computed incrementally from previously decoded packets. After a 
    /// seek, the correct granule position must be set via TH_DECCTL_SET_GRANPOS for this to work properly.</param>
    /// <returns>
    /// 0, Success. A new decoded frame can be retrieved by calling th_decode_ycbcr_out().
    /// TH_DUPFRAME, The packet represented a dropped(0-byte) frame. The player can skip the call to th_decode_ycbcr_out(), as the
    ///     contents of the decoded frame buffer have not changed.
    /// TH_EFAULT, _dec or _op was IntPtr.Zero.
    /// TH_EBADPACKET, _op does not contain encoded video data.
    /// TH_EIMPL, The video data uses bitstream features which thislibrary does not support.</returns>
    [DllImport("libtheora", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, EntryPoint = "th_decode_packetin")]
    private static extern int th_decode_packetin(th_dec_ctx_ptr _dec, ogg_packet_ptr _op, ref Int64 _granpos);

    /*
     * Function definition taken from libtheora's theoradec.h. Calls libtheora's source.
     */
    /// <summary>
    /// Outputs the next available frame of decoded Y'CbCr data. If a striped decode callback has been set with TH_DECCTL_SET_STRIPE_CB,
    /// then the application does not need to call this function.
    /// </summary>
    /// <param name="_dec">A th_dec_ctx handle.</param>
    /// <param name="_ycbcr">A video buffer structure to fill in. libtheoradec will fill in all the members of this structure, including
    /// the pointers to the uncompressed video data. The memory for this video data is owned by libtheoradec. It may be freed or 
    /// overwritten without notification when subsequent frames are decoded.</param>
    /// <returns>
    /// 0, Success
    /// TH_EFAULT, _dec or _ycbcr was IntPtr.Zero.
    /// </returns>
    [DllImport("libtheora", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, EntryPoint = "th_decode_ycbcr_out")]
    private static extern int th_decode_ycbcr_out(th_dec_ctx_ptr _dec, Th_Img_Plane[] _ycbcr);

    /*
     * Function definition taken from libtheora's theoradec.h. Calls libtheora's source.
     */
    /// <summary>
    /// Frees an allocated decoder instance.
    /// </summary>
    /// <param name="_dec">A th_dec_ctx handle.</param>
    [DllImport("libtheora", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, EntryPoint = "th_decode_free")]
    private static extern void th_decode_free(th_dec_ctx_ptr _dec);

    /// <summary>
    /// State of the Theora decoding process
    /// </summary>
    private enum State { DECODE_HEADER, DECODE_FRAME, END_OF_STREAM };
    private State state = State.DECODE_HEADER;

    /// <summary>
    /// ROS Node handle
    /// </summary>
    private NodeHandle nh = null;

    /// <summary>
    /// Theora image transport packet subscriber
    /// </summary>
    private Subscriber<ti.Packet> imageSub;

    /// <summary>
    /// The current ogg packet
    /// </summary>
    private Ogg_Packet op;
    /// <summary>
    /// Pointer to the current ogg packet
    /// </summary>
    private ogg_packet_ptr op_ptr;

    /// <summary>
    /// Contains Theora bitstream information
    /// </summary>
    private Th_Info ti;

    /// <summary>
    /// Contains Theora comment information
    /// </summary>
    private Th_Comment tc;

    /// <summary>
    /// Contains Theora setup information
    /// </summary>
    private th_setup_info_ptr setup_ptr;

    /// <summary>
    /// The decoder context
    /// </summary>
    private th_dec_ctx_ptr ctx;

    /// <summary>
    /// The granule position of the decoded packet
    /// </summary>
    private Int64 granpos;

    /// <summary>
    /// The most recent frame of decoded Y'CbCr data
    /// </summary>
    private Th_Img_Plane[] ycbcr_buf = new Th_Img_Plane[3];

    /// <summary>
    /// The most recent frame of bgr data
    /// </summary>
    private byte[] bgr;

    /// <summary>
    /// The number of bytes per pixel
    /// </summary>
    private int bytes_per_pixel;

    /// <summary>
    /// The width of the image in pixels
    /// </summary>
    private int width;

    /// <summary>
    /// The height of the image in pixels
    /// </summary>
    private int height;

    /// <summary>
    /// The texture to be applied to the attached game object
    /// </summary>
    private Texture2D texture;

    /// <summary>
    /// Event used to protect the bgr array from being modified while it is being displayed
    /// </summary>
    private AutoResetEvent textureMutex = new AutoResetEvent(true);

    /// <summary>
    /// Event used to protect the bgr array from being displayed while it is being modified
    /// </summary>
    private AutoResetEvent dataMutex = new AutoResetEvent(false);


    /// <summary>
    /// The Callback for the theora image transpot subscriber
    /// </summary>
    /// <param name="packet">The packet received from the theora image transport topic</param>
    private void ImageCb(ti.Packet packet)
    {
        //Init ogg packet
        op.packet = new byte[packet.data.Length];
        Buffer.BlockCopy(packet.data, 0, op.packet, 0, packet.data.Length);
        op.b_o_s = packet.b_o_s;
        op.e_o_s = packet.e_o_s;
        op.granulepos = packet.granulepos;
        op.packetno = packet.packetno;
        op.bytes = packet.data.Length;

        //Create Ptr to ogg packet
        op_ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Ogg_Packet)));
        Marshal.StructureToPtr(op, op_ptr, false);
        
        //If BOS reset
        if (packet.b_o_s == 1)
        {
            if (ctx != IntPtr.Zero)
            {
                th_decode_free(ctx);
                ctx = IntPtr.Zero;
            }

            th_setup_free(setup_ptr);
            setup_ptr = IntPtr.Zero;

            th_info_clear(ref ti);
            th_info_init(ref ti);

            th_comment_clear(ref tc);
            th_comment_init(ref tc);

            state = State.DECODE_HEADER;
        }
        
        //State of Theora Decode
        switch (state)
        {
            case State.DECODE_HEADER:
                DecodeHeader();
                break;
            case State.DECODE_FRAME:
                DecodeFrame();
                break;
            case State.END_OF_STREAM:
                return;
            default:
                break;
        }

        //Check for EOS
        if (packet.e_o_s == 1)
            state = State.END_OF_STREAM;

        //Free ogg ptr
        Marshal.FreeHGlobal(op_ptr);
        op_ptr = IntPtr.Zero;
    }

    /// <summary>
    /// Decodes a header packet
    /// </summary>
    private void DecodeHeader()
    {
        switch ((Ret_Values)th_decode_headerin(ref ti, ref tc, ref setup_ptr, op_ptr))
        {
            case 0:
                //Setup decode context
                ctx = th_decode_alloc(ref ti, setup_ptr);
                if (ctx == IntPtr.Zero) Debug.LogError("[TheoraSubscriber] th_decode_alloc: Invalid parameters. Unable to allocate decoder instance.");

                //Free setup info
                th_setup_free(setup_ptr);
                setup_ptr = IntPtr.Zero;

                DecodeFrame();
                state = State.DECODE_FRAME;
                break;
            case Ret_Values.TH_EFAULT:
                Debug.LogError("[TheoraSubscriber] TH_EFAULT: An invalid pointer was provided.");
                break;
            case Ret_Values.TH_EBADHEADER:
                Debug.LogError("[TheoraSubscriber] TH_EBADHEADER: The contents of the header were incomplete, invalid, or unexpected.");
                break;
            case Ret_Values.TH_EVERSION:
                Debug.LogError("[TheoraSubscriber] TH_EVERSION: The bitstream version is too high.");
                break;
            case Ret_Values.TH_ENOTFORMAT:
                Debug.LogError("[TheoraSubscriber] TH_ENOTFORMAT: The header does not belong to a Theora stream.");
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Decodes a packet containing frame data
    /// </summary>
    private void DecodeFrame()
    {
        switch ((Ret_Values)th_decode_packetin(ctx, op_ptr, ref granpos))
        {
            case 0:
                switch ((Ret_Values)th_decode_ycbcr_out(ctx, ycbcr_buf))
                {
                    case 0:
                        break;
                    case Ret_Values.TH_EFAULT:
                        if (ctx == IntPtr.Zero)
                            Debug.LogError("[TheoraSubscriber] TH_EFAULT: Decode context was NULL");
                        if (ycbcr_buf == null)
                            Debug.LogError("[TheoraSubscriber] TH_EFAULT: YUV buffer was NULL");
                        break;
                    default:
                        Debug.LogError("[TheoraSubscriber] th_decode_ycbcr_out: Did not return an acceptable value");
                        break;
                }
                break;
            case Ret_Values.TH_DUPFRAME:
                return;//Empty frame so do nothing
            case Ret_Values.TH_EFAULT:
                if (ctx == IntPtr.Zero)
                    Debug.LogError("[TheoraSubscriber] TH_EFAULT: Decode context was NULL");
                if (op_ptr == IntPtr.Zero)
                    Debug.LogError("[TheoraSubscriber] TH_EFAULT: Ogg Packet Ptr was NULL");
                break;
            case Ret_Values.TH_EBADPACKET:
                Debug.LogError("[TheoraSubscriber] TH_EBADPACKET: Ogg Packet does not contain encoded video data");
                break;
            case Ret_Values.TH_EIMPL:
                Debug.LogError("[TheoraSubscriber] TH_EIMPL: The video data uses bitstream features which thislibrary does not support");
                break;
            default:
                Debug.LogError("[TheoraSubscriber] th_decode_packetin: Did not return an acceptable value");
                break;
        }
      
        int frame_size = ycbcr_buf[0].stride * ycbcr_buf[0].height;
        int u_size = ycbcr_buf[1].stride * ycbcr_buf[1].height;
        int v_size = ycbcr_buf[2].stride * ycbcr_buf[2].height;
        width = ycbcr_buf[0].width;
        height = ycbcr_buf[0].height;

        byte[] ycbcr = new byte[frame_size + u_size + v_size];
        
        Marshal.Copy(ycbcr_buf[0].data, ycbcr, 0, frame_size);
        Marshal.Copy(ycbcr_buf[1].data, ycbcr, frame_size, u_size);
        Marshal.Copy(ycbcr_buf[2].data, ycbcr, frame_size + u_size, v_size);

        if (textureMutex.WaitOne(0))
        {
            bgr = new byte[bytes_per_pixel * frame_size];

            switch (ti.pixel_fmt)
            {
                case Th_Pixel_Fmt.TH_PF_420:
                    YUV420ToBGR(ref bgr, ref ycbcr, width, height, ycbcr_buf[0].stride);
                    break;
                case Th_Pixel_Fmt.TH_PF_422:
                    Debug.LogWarning("[TheoraSubscriber] DecodeFrame: Pixel format YUV422 not tested yet.");
                    YUV422ToBGR(ref bgr, ref ycbcr, width, height, ycbcr_buf[0].stride);
                    break;
                case Th_Pixel_Fmt.TH_PF_444:
                    Debug.LogWarning("[TheoraSubscriber] DecodeFrame: Pixel format YUV444 not tested yet.");
                    YUV444ToBGR(ref bgr, ref ycbcr, width, height, ycbcr_buf[0].stride);
                    break;
                case Th_Pixel_Fmt.TH_PF_NFORMATS:
                    Debug.LogError("[TheoraSubscriber] DecodeFrame: Pixel format not supported.");
                    break;
                default:
                    Debug.LogError("[TheoraSubscriber] DecodeFrame: Unrecognized pixel format");
                    break;
            }

            dataMutex.Set();
        }
    }

    /// <summary>
    /// Update the texture of the game object. Update is called once per frame.
    /// </summary>
    private void Update()
    {
        if (dataMutex.WaitOne(0))
        {
            texture.Resize(width, height);
            texture.LoadRawTextureData(bgr);
            texture.Apply();
            Obj.GetComponent<MeshRenderer>().material.mainTexture = texture;
            textureMutex.Set();
        }
    }

    /// <summary>
    /// Used for initialization
    /// </summary>
    private void Start()
    {
        //ROS checks
        if (roscore == null)
        {
            Debug.LogWarning("[TheoraSubscriber] " + name + " does not have a roscore specified! Will try to find one!");
            roscore = FindObjectOfType<ROSCore>();
        }
        if (roscore == null)
        {
            Debug.LogError("[TheoraSubscriber] " + name + " can not find an instance of ROSCore! Turning off ImageSub!");
            enabled = false;
            return;
        }
        nh = roscore.getNodeHandle();
        if (nh == null)
        {
            Debug.LogError("[TheoraSubscriber] Ros Node handle is null! Turning off ImageSub!");
            enabled = false;
            return;
        }

        imageSub = nh.subscribe<ti.Packet>(Topic, 0, ImageCb);

        granpos = 0;
        width = 0;
        height = 0;
        bytes_per_pixel = 3;

        texture = new Texture2D(0, 0, TextureFormat.RGB24, false); //This texture format seems to be BGR not RGB
    }

    /// <summary>
    /// Free allocated memory
    /// </summary>
    private void OnDestroy()
    {
        state = State.END_OF_STREAM;

        if (imageSub != null)
            imageSub.unsubscribe();

        th_info_clear(ref ti);
        th_comment_clear(ref tc);

        if (op_ptr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(op_ptr);
            op_ptr = IntPtr.Zero;
        }
        if (setup_ptr != IntPtr.Zero)
        {
            th_setup_free(setup_ptr);
            Marshal.FreeHGlobal(setup_ptr);
            setup_ptr = IntPtr.Zero;
        }
        if (ctx != IntPtr.Zero)
        {
            th_decode_free(ctx);
            ctx = IntPtr.Zero;
        }
    }

    //TODO: Only convert the new pixels to save on cpu usage
    //TODO: Make this one function
    //Taken from stackoverflow: https://stackoverflow.com/questions/12469730/confusion-on-yuv-nv21-conversion-to-rgb
    /// <summary>
    /// Convert a YUV420 byte[] to bgr byte[]
    /// </summary>
    /// <param name="bgr">A reference to the bgr byte[] to fill. This must be initialized to an appropriate size.</param>
    /// <param name="yuv420sp">A reference to the YUV byte[] to convert</param>
    /// <param name="width">The width of the frame in pixels</param>
    /// <param name="height">The height of the frame in pixels</param>
    /// <param name="stride">The stride of the YUV buffer in bytes</param>
    private void YUV420ToBGR(ref byte[] bgr, ref byte[] yuv420sp, int width, int height, int stride)
    {
        int frameSize = stride * height;
        int a = 0;

        for (int i = 0; i < height; i++)
            for (int j = 0; j < width; j++)
            {
                int y = (0xff & ((int)yuv420sp[i * stride + j]));
                int u = (0xff & ((int)yuv420sp[frameSize + ((i & ~1) >> 1) * (stride >> 1) + ((j & ~1) >> 1)]));
                int v = (0xff & ((int)yuv420sp[(frameSize + frameSize / 4) + ((i & ~1) >> 1) * (stride >> 1) + ((j & ~1) >> 1)]));
                y = y < 16 ? 16 : y;

                int r = (int)(1.164f * (y - 16) + 1.596f * (u - 128));
                int g = (int)(1.164f * (y - 16) - 0.813f * (u - 128) - 0.391f * (v - 128));
                int b = (int)(1.164f * (y - 16) + 2.018f * (v - 128));

                r = r < 0 ? 0 : (r > 255 ? 255 : r);
                g = g < 0 ? 0 : (g > 255 ? 255 : g);
                b = b < 0 ? 0 : (b > 255 ? 255 : b);

                //Unity's Texture format RGB24 seems to be BGR not RGB
                bgr[a++] = (byte)b;
                bgr[a++] = (byte)g;
                bgr[a++] = (byte)r;
            }
    }

    //Taken from stackoverflow: https://stackoverflow.com/questions/12469730/confusion-on-yuv-nv21-conversion-to-rgb
    /// <summary>
    /// Convert a YUV422 byte[] to bgr byte[]
    /// </summary>
    /// <param name="bgr">A reference to the bgr byte[] to fill. This must be initialized to an appropriate size.</param>
    /// <param name="yuv422sp">A reference to the YUV byte[] to convert</param>
    /// <param name="width">The width of the frame in pixels</param>
    /// <param name="height">The height of the frame in pixels</param>
    /// <param name="stride">The stride of the YUV buffer in bytes</param>
    private void YUV422ToBGR(ref byte[] bgr, ref byte[] yuv422sp, int width, int height, int stride)
    {
        int frameSize = stride * height;
        int a = 0;

        for (int i = 0; i < height; i++)
            for (int j = 0; j < width; j++)
            {
                int y = (0xff & ((int)yuv422sp[i * stride + j]));
                int u = (0xff & ((int)yuv422sp[frameSize + i * (stride >> 1) + ((j & ~1) >> 1)]));
                int v = (0xff & ((int)yuv422sp[(frameSize + frameSize / 2) + i * (stride >> 1) + ((j & ~1) >> 1)]));
                y = y < 16 ? 16 : y;

                int r = (int)(1.164f * (y - 16) + 1.596f * (u - 128));
                int g = (int)(1.164f * (y - 16) - 0.813f * (u - 128) - 0.391f * (v - 128));
                int b = (int)(1.164f * (y - 16) + 2.018f * (v - 128));

                r = r < 0 ? 0 : (r > 255 ? 255 : r);
                g = g < 0 ? 0 : (g > 255 ? 255 : g);
                b = b < 0 ? 0 : (b > 255 ? 255 : b);

                //Unity's Texture format RGB24 seems to be BGR not RGB
                bgr[a++] = (byte)b;
                bgr[a++] = (byte)g;
                bgr[a++] = (byte)r;

            }
    }

    //Taken from stackoverflow: https://stackoverflow.com/questions/12469730/confusion-on-yuv-nv21-conversion-to-rgb
    /// <summary>
    /// Convert a YUV444 byte[] to bgr byte[]
    /// </summary>
    /// <param name="bgr">A reference to the bgr byte[] to fill. This must be initialized to an appropriate size.</param>
    /// <param name="yuv444sp">A reference to the YUV byte[] to convert</param>
    /// <param name="width">The width of the frame in pixels</param>
    /// <param name="height">The height of the frame in pixels</param>
    /// <param name="stride">The stride of the YUV buffer in bytes</param>
    private void YUV444ToBGR(ref byte[] bgr, ref byte[] yuv444sp, int width, int height, int stride)
    {
        int frameSize = stride * height;
        int a = 0;

        for (int i = 0; i < height; i++)
            for (int j = 0; j < width; j++)
            {
                int y = (0xff & ((int)yuv444sp[i * stride + j]));
                int u = (0xff & ((int)yuv444sp[frameSize + i * stride + j]));
                int v = (0xff & ((int)yuv444sp[(frameSize * 2) + i * stride + j]));
                y = y < 16 ? 16 : y;

                int r = (int)(1.164f * (y - 16) + 1.596f * (u - 128));
                int g = (int)(1.164f * (y - 16) - 0.813f * (u - 128) - 0.391f * (v - 128));
                int b = (int)(1.164f * (y - 16) + 2.018f * (v - 128));

                r = r < 0 ? 0 : (r > 255 ? 255 : r);
                g = g < 0 ? 0 : (g > 255 ? 255 : g);
                b = b < 0 ? 0 : (b > 255 ? 255 : b);

                //Unity's Texture format RGB24 seems to be BGR not RGB
                bgr[a++] = (byte)b;
                bgr[a++] = (byte)g;
                bgr[a++] = (byte)r;

            }
    }
}
