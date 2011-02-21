/*
This file is part of PacketDotNet

PacketDotNet is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

PacketDotNet is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public License
along with PacketDotNet.  If not, see <http://www.gnu.org/licenses/>.
*/
/*
 * Copyright 2009 David Bond <mokon@mokon.net>
 * Copyright 2009 Chris Morgan <chmorgan@gmail.com>
 */

using System;
using System.IO;
using MiscUtil.Conversion;
using PacketDotNet.Utils;

namespace PacketDotNet
{
    /// <summary>
    /// IPv6 packet
    /// 
    /// References
    /// ----------
    /// http://tools.ietf.org/html/rfc2460
    /// http://en.wikipedia.org/wiki/IPv6
    /// </summary>
    public class IPv6Packet : IpPacket
    {
#if DEBUG
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
#else
        // NOTE: No need to warn about lack of use, the compiler won't
        //       put any calls to 'log' here but we need 'log' to exist to compile
#pragma warning disable 0169
        private static readonly ILogInactive log;
#pragma warning restore 0169
#endif

        /// <value>
        /// Minimum number of bytes in an IPv6 header
        /// </value>
        public const int HeaderMinimumLength = 40;

        /// <value>
        /// The version of the IP protocol. The '6' in IPv6 indicates the version of the protocol
        /// </value>
        public static IpVersion ipVersion = IpVersion.IPv6;

        private Int32 VersionTrafficClassFlowLabel
        {
            get
            {
                return EndianBitConverter.Big.ToInt32(header.Bytes,
                                                      header.Offset + IPv6Fields.VersionTrafficClassFlowLabelPosition);
            }

            set
            {
                EndianBitConverter.Big.CopyBytes(value, header.Bytes, header.Offset + IPv6Fields.VersionTrafficClassFlowLabelPosition);
            }
        }

        /// <summary>
        /// The version field of the IPv6 Packet.
        /// </summary>
        public override IpVersion Version
        {
            get
            {
                return (IpVersion)((VersionTrafficClassFlowLabel >> 28) & 0xF);
            }

            set
            {
                var theValue = (Int32)value;

                // read the existing value
                var field = (UInt32)VersionTrafficClassFlowLabel;

                // mask the new field into place
                field = (UInt32)((field & 0x0FFFFFFF) | ((theValue << 28) & 0xF0000000));

                // write the updated value back
                VersionTrafficClassFlowLabel = (int)field;
            }
        }

        /// <summary>
        /// The traffic class field of the IPv6 Packet.
        /// </summary>
        public virtual int TrafficClass
        {
            get
            {
                return ((VersionTrafficClassFlowLabel >> 20) & 0xFF);
            }

            set
            {
                // read the original value
                var field = (UInt32)VersionTrafficClassFlowLabel;

                // mask in the new field
                field = (UInt32)(((field & 0xF00FFFFF) | (((UInt32)value) << 20 ) & 0x0FF00000));

                // write the updated value back
                VersionTrafficClassFlowLabel = (int)field;
            }
        }

        /// <summary>
        /// The flow label field of the IPv6 Packet.
        /// </summary>
        public virtual int FlowLabel
        {
            get
            {
                return (VersionTrafficClassFlowLabel & 0xFFFFF);
            }

            set
            {
                // read the original value
                var field = (UInt32)VersionTrafficClassFlowLabel;

                // make the value in
                field = (UInt32)((field & 0xFFF00000) | ((UInt32)(value) & 0x000FFFFF));

                // write the updated value back
                VersionTrafficClassFlowLabel = (int)field;
            }
        }

        /// <summary>
        /// The payload lengeth field of the IPv6 Packet
        /// NOTE: Differs from the IPv4 'Total length' field that includes the length of the header as
        ///       payload length is ONLY the size of the payload.
        /// </summary>
        public override ushort PayloadLength
        {
            get
            {
                return EndianBitConverter.Big.ToUInt16(header.Bytes,
                                                      header.Offset + IPv6Fields.PayloadLengthPosition);
            }

            set
            {
                EndianBitConverter.Big.CopyBytes(value,
                                                 header.Bytes,
                                                 header.Offset + IPv6Fields.PayloadLengthPosition);
            }
        }

        /// <value>
        /// Backwards compatibility property for IPv4.HeaderLength
        /// NOTE: This field is the number of 32bit words
        /// </value>
        public override int HeaderLength
        {
            get
            {
                return (IPv6Fields.HeaderLength / 4);
            }

            set
            {
                throw new System.NotImplementedException ();
            }
        }

        /// <value>
        /// Backwards compatibility property for IPv4.TotalLength
        /// </value>
        public override int TotalLength
        {
            get
            {
                return PayloadLength + (HeaderLength * 4);
            }

            set
            {
                PayloadLength = (ushort)(value - (HeaderLength * 4));
            }
        }

        /// <summary>
        /// Identifies the protocol encapsulated by this packet
        /// 
        /// Replaces IPv4's 'protocol' field, has compatible values
        /// </summary>
        public override IPProtocolType NextHeader
        {
            get
            {
                return (IPProtocolType)(header.Bytes[header.Offset + IPv6Fields.NextHeaderPosition]);
            }

            set
            {
                header.Bytes[header.Offset + IPv6Fields.NextHeaderPosition] = (byte)value;
            }
        }

        /// <value>
        /// The protocol of the packet encapsulated in this ip packet
        /// </value>
        public override IPProtocolType Protocol
        {
            get { return NextHeader; }
            set { NextHeader = value; }
        }

        /// <summary>
        /// The hop limit field of the IPv6 Packet.
        /// NOTE: Replaces the 'time to live' field of IPv4
        /// 
        /// 8-bit value
        /// </summary>
        public override int HopLimit
        {
            get
            {
                return header.Bytes[header.Offset + IPv6Fields.HopLimitPosition];
            }

            set
            {
                header.Bytes[header.Offset + IPv6Fields.HopLimitPosition] = (byte)value;
            }
        }

        /// <value>
        /// Helper alias for 'HopLimit'
        /// </value>
        public override int TimeToLive
        {
            get { return HopLimit; }
            set { HopLimit = value; }
        }

        /// <summary>
        /// The source address field of the IPv6 Packet.
        /// </summary>
        public override System.Net.IPAddress SourceAddress
        {
            get
            {
                return IpPacket.GetIPAddress(System.Net.Sockets.AddressFamily.InterNetworkV6,
                                             header.Offset + IPv6Fields.SourceAddressPosition,
                                             header.Bytes);
            }

            set
            {
                byte[] address = value.GetAddressBytes();
                System.Array.Copy(address, 0,
                                  header.Bytes, header.Offset + IPv6Fields.SourceAddressPosition,
                                  address.Length);
            }
        }

        /// <summary>
        /// The destination address field of the IPv6 Packet.
        /// </summary>
        public override System.Net.IPAddress DestinationAddress
        {
            get
            {
                return IpPacket.GetIPAddress(System.Net.Sockets.AddressFamily.InterNetworkV6,
                                             header.Offset + IPv6Fields.DestinationAddressPosition,
                                             header.Bytes);
            }

            set
            {
                byte[] address = value.GetAddressBytes();
                System.Array.Copy(address, 0,
                                  header.Bytes, header.Offset + IPv6Fields.DestinationAddressPosition,
                                  address.Length);
            }
        }

        /// <summary>
        /// Create an IPv6 packet from values
        /// </summary>
        /// <param name="SourceAddress">
        /// A <see cref="System.Net.IPAddress"/>
        /// </param>
        /// <param name="DestinationAddress">
        /// A <see cref="System.Net.IPAddress"/>
        /// </param>
        public IPv6Packet(System.Net.IPAddress SourceAddress,
                          System.Net.IPAddress DestinationAddress)
            : base(new PosixTimeval())
        {
            log.Debug("");

            // allocate memory for this packet
            int offset = 0;
            int length = IPv6Fields.HeaderLength;
            var headerBytes = new byte[length];
            header = new ByteArraySegment(headerBytes, offset, length);

            // set some default values to make this packet valid
            PayloadLength = 0;
            TimeToLive = DefaultTimeToLive;

            // set instance values
            this.SourceAddress = SourceAddress;
            this.DestinationAddress = DestinationAddress;
            this.Version = ipVersion;
        }

        /// <summary>
        /// byte[]/int offset constructor, timeval defaults to the current time
        /// </summary>
        /// <param name="Bytes">
        /// A <see cref="System.Byte"/>
        /// </param>
        /// <param name="Offset">
        /// A <see cref="System.Int32"/>
        /// </param>
        public IPv6Packet(byte[] Bytes, int Offset) :
            this(Bytes, Offset, new PosixTimeval())
        {
            log.Debug("");
        }

        /// <summary>
        /// byte[]/int offset/PosixTimeval constructor
        /// </summary>
        /// <param name="Bytes">
        /// A <see cref="System.Byte"/>
        /// </param>
        /// <param name="Offset">
        /// A <see cref="System.Int32"/>
        /// </param>
        /// <param name="Timeval">
        /// A <see cref="PosixTimeval"/>
        /// </param>
        public IPv6Packet(byte[] Bytes, int Offset, PosixTimeval Timeval) :
            base(Timeval)
        {
            log.DebugFormat("Bytes.Length {0}, Offset {1}",
                            Bytes.Length,
                            Offset);

            // slice off the header
            header = new ByteArraySegment(Bytes, Offset, IPv6Packet.HeaderMinimumLength);

            // set the actual length, we need to do this because we need to set
            // header to something valid above before we can retrieve the PayloadLength
            log.DebugFormat("PayloadLength: {0}", PayloadLength);
            header.Length = (Bytes.Length - Offset) - PayloadLength;

            // parse the payload
            payloadPacketOrData = IpPacket.ParseEncapsulatedBytes(header,
                                                                  NextHeader,
                                                                  Timeval,
                                                                  this);
        }

        /// <summary>
        /// Prepend to the given byte[] origHeader the portion of the IPv6 header used for
        /// generating an tcp checksum
        ///
        /// http://en.wikipedia.org/wiki/Transmission_Control_Protocol#TCP_checksum_using_IPv6
        /// http://tools.ietf.org/html/rfc2460#page-27
        /// </summary>
        /// <param name="origHeader">
        /// A <see cref="System.Byte"/>
        /// </param>
        /// <returns>
        /// A <see cref="System.Byte"/>
        /// </returns>
        internal override byte[] AttachPseudoIPHeader(byte[] origHeader)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            // 0-16: ip src addr
            bw.Write(header.Bytes, header.Offset + IPv6Fields.SourceAddressPosition,
                     IPv6Fields.AddressLength);

            // 17-32: ip dst addr
            bw.Write(header.Bytes, header.Offset + IPv6Fields.DestinationAddressPosition,
                     IPv6Fields.AddressLength);

            // 33-36: TCP length
            bw.Write((UInt32)System.Net.IPAddress.HostToNetworkOrder((Int32)origHeader.Length));

            // 37-39: 3 bytes of zeros
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((byte)0);

            // 40: Next header
            bw.Write((byte)NextHeader);

            // prefix the pseudoHeader to the header+data
            byte[] pseudoHeader = ms.ToArray();
            int headerSize = pseudoHeader.Length + origHeader.Length;
            bool odd = origHeader.Length % 2 != 0;
            if (odd)
                headerSize++;

            byte[] finalData = new byte[headerSize];

            // copy the pseudo header in
            Array.Copy(pseudoHeader, 0, finalData, 0, pseudoHeader.Length);

            // copy the origHeader in
            Array.Copy(origHeader, 0, finalData, pseudoHeader.Length, origHeader.Length);

            //if not even length, pad with a zero
            if (odd)
                finalData[finalData.Length - 1] = 0;

            return finalData;
        }

        /// <summary>
        /// Converts the packet to a string.
        /// </summary>
        /// <returns></returns>
        public override String ToString( )
        {
            return base.ToString( ) + "\r\nIPv6 Packet [\r\n"
                   + "\tIPv6 Source Address: " + SourceAddress.ToString() + ", \r\n"
                   + "\tIPv6 Destination Address: " + DestinationAddress.ToString() + "\r\n"
                   + "]";
            // TODO Implement Better ToString
        }

        /// <summary> Generate string with contents describing this IP packet.</summary>
        /// <param name="colored">whether or not the string should contain ansi
        /// color escape sequences.
        /// </param>
        public override System.String ToColoredString(bool colored)
        {
            System.Text.StringBuilder buffer = new System.Text.StringBuilder();
            buffer.Append('[');
            if (colored)
                buffer.Append(Color);
            buffer.Append("IPv6Packet");
            if (colored)
                buffer.Append(AnsiEscapeSequences.Reset);
            buffer.Append(": ");
            buffer.Append(SourceAddress + " -> " + DestinationAddress);
            buffer.Append(" next header=" + NextHeader);
            //FIXME: figure out what to use for the lengths
#if false
            buffer.Append(" l=" + this.IPPayloadLength);
            buffer.Append(" sum=" + this.IPPayloadLength);
#endif
            buffer.Append(']');

            // append the base class output
            buffer.Append(base.ToColoredString(colored));

            return buffer.ToString();
        }

        /// <summary> Convert this IP packet to a more verbose string.</summary>
        public override System.String ToColoredVerboseString(bool colored)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Converts the packet to a color string. TODO add a method for colored to string.
        /// </summary>
        override public String Color
        {
            get
            {
                return AnsiEscapeSequences.White;
            }
        }

        /// <summary>
        /// Generate a random packet
        /// </summary>
        /// <returns>
        /// A <see cref="Packet"/>
        /// </returns>
        public static IPv6Packet RandomPacket()
        {
            var srcAddress = RandomUtils.GetIPAddress(ipVersion);
            var dstAddress = RandomUtils.GetIPAddress(ipVersion);
            return new IPv6Packet(srcAddress, dstAddress);
        }
    }
}
