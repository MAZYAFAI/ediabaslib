﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BMW.Rheingold.Psdz.Model.SecureCoding.SignatureResultCto
{
    public interface IPsdzSignatureResultCto
    {
        IPsdzSgbmId Cafd { get; }

        string BootloaderSgbmNumber { get; }

        byte[] Signature { get; }

        int[] CodingProofStamp { get; }

        string KeyAlgorithm { get; }

        string Digest { get; }
    }
}
