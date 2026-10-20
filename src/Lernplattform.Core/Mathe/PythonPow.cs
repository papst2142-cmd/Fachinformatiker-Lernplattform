namespace Lernplattform.Core.Mathe;

/// <summary>
/// Potenz x^y bitgenau wie Python <c>x ** y</c> unter Linux (glibc 2.28 und neuer,
/// Rechner mit FMA). Python rechnet <c>**</c> mit der Funktion pow der Systembibliothek;
/// die Vergleichswerte entstehen unter Linux. <see cref="Math.Pow"/> nutzt dagegen die
/// Bibliothek des jeweiligen Systems und weicht unter Windows und macOS in der letzten
/// Stelle ab (CI-Lauf 39406464451, Farbabstand). Diese Fassung rechnet ueberall gleich.
///
/// Nachbau von math/pow.c, pow_log_data.c und exp_data.c aus ARM Optimized Routines
/// (github.com/ARM-software/optimized-routines, Stand f67e7da), dem Ursprung der
/// glibc-Funktion. Die Rechenschritte mit fma folgen genau dem, was gcc -O2 -mfma
/// daraus macht (glibc-Variante __pow_fma); geprueft mit 26 Mio. Werten gegen glibc 2.39.
/// Tabellen als Bitmuster aus den uebersetzten Original-Tabellen.
///
/// Copyright (c) 2018-2026, Arm Limited. SPDX-License-Identifier: MIT OR Apache-2.0 WITH
/// LLVM-exception. Genutzt unter MIT; Lizenztext in THIRD_PARTY_NOTICES.txt.
/// </summary>
public static class PythonPow
{
    private const ulong Ln2Hi = 0x3FE62E42FEFA3800, Ln2Lo = 0x3D2EF35793C76730;
    private static readonly ulong[] LogPoly =
    [
        0xBFE0000000000000, 0xBFE5555555555560, 0x3FE0000000000006, 0x3FE999999959554E, 0xBFE555555529A47A, 0xBFF2495B9B4845E9, 0x3FF0002B8B263FC3,
    ];

    // je Eintrag: invc, logc, logctail
    private static readonly ulong[] LogTab =
    [
        0x3FF6A00000000000, 0xBFD62C82F2B9C800, 0x3CFAB42428375680,
        0x3FF6800000000000, 0xBFD5D1BDBF580800, 0xBD1CA508D8E0F720,
        0x3FF6600000000000, 0xBFD5767717455800, 0xBD2362A4D5B6506D,
        0x3FF6400000000000, 0xBFD51AAD872DF800, 0xBCE684E49EB067D5,
        0x3FF6200000000000, 0xBFD4BE5F95777800, 0xBD041B6993293EE0,
        0x3FF6000000000000, 0xBFD4618BC21C6000, 0x3D13D82F484C84CC,
        0x3FF5E00000000000, 0xBFD404308686A800, 0x3CDC42F3ED820B3A,
        0x3FF5C00000000000, 0xBFD3A64C55694800, 0x3D20B1C686519460,
        0x3FF5A00000000000, 0xBFD347DD9A988000, 0x3D25594DD4C58092,
        0x3FF5800000000000, 0xBFD2E8E2BAE12000, 0x3D267B1E99B72BD8,
        0x3FF5600000000000, 0xBFD2895A13DE8800, 0x3D15CA14B6CFB03F,
        0x3FF5600000000000, 0xBFD2895A13DE8800, 0x3D15CA14B6CFB03F,
        0x3FF5400000000000, 0xBFD22941FBCF7800, 0xBD165A242853DA76,
        0x3FF5200000000000, 0xBFD1C898C1699800, 0xBD1FAFBC68E75404,
        0x3FF5000000000000, 0xBFD1675CABABA800, 0x3D1F1FC63382A8F0,
        0x3FF4E00000000000, 0xBFD1058BF9AE4800, 0xBD26A8C4FD055A66,
        0x3FF4C00000000000, 0xBFD0A324E2739000, 0xBD0C6BEE7EF4030E,
        0x3FF4A00000000000, 0xBFD0402594B4D000, 0xBCF036B89EF42D7F,
        0x3FF4A00000000000, 0xBFD0402594B4D000, 0xBCF036B89EF42D7F,
        0x3FF4800000000000, 0xBFCFB9186D5E4000, 0x3D0D572AAB993C87,
        0x3FF4600000000000, 0xBFCEF0ADCBDC6000, 0x3D2B26B79C86AF24,
        0x3FF4400000000000, 0xBFCE27076E2AF000, 0xBD172F4F543FFF10,
        0x3FF4200000000000, 0xBFCD5C216B4FC000, 0x3D21BA91BBCA681B,
        0x3FF4000000000000, 0xBFCC8FF7C79AA000, 0x3D27794F689F8434,
        0x3FF4000000000000, 0xBFCC8FF7C79AA000, 0x3D27794F689F8434,
        0x3FF3E00000000000, 0xBFCBC286742D9000, 0x3D194EB0318BB78F,
        0x3FF3C00000000000, 0xBFCAF3C94E80C000, 0x3CBA4E633FCD9066,
        0x3FF3A00000000000, 0xBFCA23BC1FE2B000, 0xBD258C64DC46C1EA,
        0x3FF3A00000000000, 0xBFCA23BC1FE2B000, 0xBD258C64DC46C1EA,
        0x3FF3800000000000, 0xBFC9525A9CF45000, 0xBD2AD1D904C1D4E3,
        0x3FF3600000000000, 0xBFC87FA06520D000, 0x3D2BBDBF7FDBFA09,
        0x3FF3400000000000, 0xBFC7AB890210E000, 0x3D2BDB9072534A58,
        0x3FF3400000000000, 0xBFC7AB890210E000, 0x3D2BDB9072534A58,
        0x3FF3200000000000, 0xBFC6D60FE719D000, 0xBD10E46AA3B2E266,
        0x3FF3000000000000, 0xBFC5FF3070A79000, 0xBD1E9E439F105039,
        0x3FF3000000000000, 0xBFC5FF3070A79000, 0xBD1E9E439F105039,
        0x3FF2E00000000000, 0xBFC526E5E3A1B000, 0xBD20DE8B90075B8F,
        0x3FF2C00000000000, 0xBFC44D2B6CCB8000, 0x3D170CC16135783C,
        0x3FF2C00000000000, 0xBFC44D2B6CCB8000, 0x3D170CC16135783C,
        0x3FF2A00000000000, 0xBFC371FC201E9000, 0x3CF178864D27543A,
        0x3FF2800000000000, 0xBFC29552F81FF000, 0xBD248D301771C408,
        0x3FF2600000000000, 0xBFC1B72AD52F6000, 0xBD2E80A41811A396,
        0x3FF2600000000000, 0xBFC1B72AD52F6000, 0xBD2E80A41811A396,
        0x3FF2400000000000, 0xBFC0D77E7CD09000, 0x3D0A699688E85BF4,
        0x3FF2400000000000, 0xBFC0D77E7CD09000, 0x3D0A699688E85BF4,
        0x3FF2200000000000, 0xBFBFEC9131DBE000, 0xBD2575545CA333F2,
        0x3FF2000000000000, 0xBFBE27076E2B0000, 0x3D2A342C2AF0003C,
        0x3FF2000000000000, 0xBFBE27076E2B0000, 0x3D2A342C2AF0003C,
        0x3FF1E00000000000, 0xBFBC5E548F5BC000, 0xBD1D0C57585FBE06,
        0x3FF1C00000000000, 0xBFBA926D3A4AE000, 0x3D253935E85BAAC8,
        0x3FF1C00000000000, 0xBFBA926D3A4AE000, 0x3D253935E85BAAC8,
        0x3FF1A00000000000, 0xBFB8C345D631A000, 0x3D137C294D2F5668,
        0x3FF1A00000000000, 0xBFB8C345D631A000, 0x3D137C294D2F5668,
        0x3FF1800000000000, 0xBFB6F0D28AE56000, 0xBD269737C93373DA,
        0x3FF1600000000000, 0xBFB51B073F062000, 0x3D1F025B61C65E57,
        0x3FF1600000000000, 0xBFB51B073F062000, 0x3D1F025B61C65E57,
        0x3FF1400000000000, 0xBFB341D7961BE000, 0x3D2C5EDACCF913DF,
        0x3FF1400000000000, 0xBFB341D7961BE000, 0x3D2C5EDACCF913DF,
        0x3FF1200000000000, 0xBFB16536EEA38000, 0x3D147C5E768FA309,
        0x3FF1000000000000, 0xBFAF0A30C0118000, 0x3D2D599E83368E91,
        0x3FF1000000000000, 0xBFAF0A30C0118000, 0x3D2D599E83368E91,
        0x3FF0E00000000000, 0xBFAB42DD71198000, 0x3D1C827AE5D6704C,
        0x3FF0E00000000000, 0xBFAB42DD71198000, 0x3D1C827AE5D6704C,
        0x3FF0C00000000000, 0xBFA77458F632C000, 0xBD2CFC4634F2A1EE,
        0x3FF0C00000000000, 0xBFA77458F632C000, 0xBD2CFC4634F2A1EE,
        0x3FF0A00000000000, 0xBFA39E87B9FEC000, 0x3CF502B7F526FEAA,
        0x3FF0A00000000000, 0xBFA39E87B9FEC000, 0x3CF502B7F526FEAA,
        0x3FF0800000000000, 0xBF9F829B0E780000, 0xBD2980267C7E09E4,
        0x3FF0800000000000, 0xBF9F829B0E780000, 0xBD2980267C7E09E4,
        0x3FF0600000000000, 0xBF97B91B07D58000, 0xBD288D5493FAA639,
        0x3FF0400000000000, 0xBF8FC0A8B0FC0000, 0xBCDF1E7CF6D3A69C,
        0x3FF0400000000000, 0xBF8FC0A8B0FC0000, 0xBCDF1E7CF6D3A69C,
        0x3FF0200000000000, 0xBF7FE02A6B100000, 0xBD19E23F0DDA40E4,
        0x3FF0200000000000, 0xBF7FE02A6B100000, 0xBD19E23F0DDA40E4,
        0x3FF0000000000000, 0x0000000000000000, 0x0000000000000000,
        0x3FF0000000000000, 0x0000000000000000, 0x0000000000000000,
        0x3FEFC00000000000, 0x3F80101575890000, 0xBD10C76B999D2BE8,
        0x3FEF800000000000, 0x3F90205658938000, 0xBD23DC5B06E2F7D2,
        0x3FEF400000000000, 0x3F98492528C90000, 0xBD2AA0BA325A0C34,
        0x3FEF000000000000, 0x3FA0415D89E74000, 0x3D0111C05CF1D753,
        0x3FEEC00000000000, 0x3FA466AED42E0000, 0xBD2C167375BDFD28,
        0x3FEE800000000000, 0x3FA894AA149FC000, 0xBD197995D05A267D,
        0x3FEE400000000000, 0x3FACCB73CDDDC000, 0xBD1A68F247D82807,
        0x3FEE200000000000, 0x3FAEEA31C006C000, 0xBD0E113E4FC93B7B,
        0x3FEDE00000000000, 0x3FB1973BD1466000, 0xBD25325D560D9E9B,
        0x3FEDA00000000000, 0x3FB3BDF5A7D1E000, 0x3D2CC85EA5DB4ED7,
        0x3FED600000000000, 0x3FB5E95A4D97A000, 0xBD2C69063C5D1D1E,
        0x3FED400000000000, 0x3FB700D30AEAC000, 0x3CEC1E8DA99DED32,
        0x3FED000000000000, 0x3FB9335E5D594000, 0x3D23115C3ABD47DA,
        0x3FECC00000000000, 0x3FBB6AC88DAD6000, 0xBD1390802BF768E5,
        0x3FECA00000000000, 0x3FBC885801BC4000, 0x3D2646D1C65AACD3,
        0x3FEC600000000000, 0x3FBEC739830A2000, 0xBD2DC068AFE645E0,
        0x3FEC400000000000, 0x3FBFE89139DBE000, 0xBD2534D64FA10AFD,
        0x3FEC000000000000, 0x3FC1178E8227E000, 0x3D21EF78CE2D07F2,
        0x3FEBE00000000000, 0x3FC1AA2B7E23F000, 0x3D2CA78E44389934,
        0x3FEBA00000000000, 0x3FC2D1610C868000, 0x3D039D6CCB81B4A1,
        0x3FEB800000000000, 0x3FC365FCB0159000, 0x3CC62FA8234B7289,
        0x3FEB400000000000, 0x3FC4913D8333B000, 0x3D25837954FDB678,
        0x3FEB200000000000, 0x3FC527E5E4A1B000, 0x3D2633E8E5697DC7,
        0x3FEAE00000000000, 0x3FC6574EBE8C1000, 0x3D19CF8B2C3C2E78,
        0x3FEAC00000000000, 0x3FC6F0128B757000, 0xBD25118DE59C21E1,
        0x3FEAA00000000000, 0x3FC7898D85445000, 0xBD1C661070914305,
        0x3FEA600000000000, 0x3FC8BEAFEB390000, 0xBD073D54AAE92CD1,
        0x3FEA400000000000, 0x3FC95A5ADCF70000, 0x3D07F22858A0FF6F,
        0x3FEA000000000000, 0x3FCA93ED3C8AE000, 0xBD28724350562169,
        0x3FE9E00000000000, 0x3FCB31D8575BD000, 0xBD0C358D4EACE1AA,
        0x3FE9C00000000000, 0x3FCBD087383BE000, 0xBD2D4BC4595412B6,
        0x3FE9A00000000000, 0x3FCC6FFBC6F01000, 0xBCF1EC72C5962BD2,
        0x3FE9600000000000, 0x3FCDB13DB0D49000, 0xBD2AFF2AF715B035,
        0x3FE9400000000000, 0x3FCE530EFFE71000, 0x3CC212276041F430,
        0x3FE9200000000000, 0x3FCEF5ADE4DD0000, 0xBCCA211565BB8E11,
        0x3FE9000000000000, 0x3FCF991C6CB3B000, 0x3D1BCBECCA0CDF30,
        0x3FE8C00000000000, 0x3FD07138604D5800, 0x3CF89CDB16ED4E91,
        0x3FE8A00000000000, 0x3FD0C42D67616000, 0x3D27188B163CEAE9,
        0x3FE8800000000000, 0x3FD1178E8227E800, 0xBD2C210E63A5F01C,
        0x3FE8600000000000, 0x3FD16B5CCBACF800, 0x3D2B9ACDF7A51681,
        0x3FE8400000000000, 0x3FD1BF99635A6800, 0x3D2CA6ED5147BDB7,
        0x3FE8200000000000, 0x3FD214456D0EB800, 0x3D0A87DEBA46BAEA,
        0x3FE7E00000000000, 0x3FD2BEF07CDC9000, 0x3D2A9CFA4A5004F4,
        0x3FE7C00000000000, 0x3FD314F1E1D36000, 0xBD28E27AD3213CB8,
        0x3FE7A00000000000, 0x3FD36B6776BE1000, 0x3D116ECDB0F177C8,
        0x3FE7800000000000, 0x3FD3C25277333000, 0x3D183B54B606BD5C,
        0x3FE7600000000000, 0x3FD419B423D5E800, 0x3D08E436EC90E09D,
        0x3FE7400000000000, 0x3FD4718DC271C800, 0xBD2F27CE0967D675,
        0x3FE7200000000000, 0x3FD4C9E09E173000, 0xBD2E20891B0AD8A4,
        0x3FE7000000000000, 0x3FD522AE0738A000, 0x3D2EBE708164C759,
        0x3FE6E00000000000, 0x3FD57BF753C8D000, 0x3D1FADEDEE5D40EF,
        0x3FE6C00000000000, 0x3FD5D5BDDF596000, 0xBD0A0B2A08A465DC,
    ];

    private const ulong InvLn2N = 0x40671547652B82FE, NegLn2HiN = 0xBF762E42FEFA0000, NegLn2LoN = 0xBD0CF79ABC9E3B3A, Shift = 0x4338000000000000;
    private static readonly ulong[] ExpPoly =
    [
        0x3FDFFFFFFFFFFDBD, 0x3FC555555555543C, 0x3FA55555CF172B91, 0x3F81111167A4D017,
    ];

    // je Eintrag: tail, sbits
    private static readonly ulong[] ExpTab =
    [
        0x0000000000000000, 0x3FF0000000000000,
        0x3C9B3B4F1A88BF6E, 0x3FEFF63DA9FB3335,
        0xBC7160139CD8DC5D, 0x3FEFEC9A3E778061,
        0xBC905E7A108766D1, 0x3FEFE315E86E7F85,
        0x3C8CD2523567F613, 0x3FEFD9B0D3158574,
        0xBC8BCE8023F98EFA, 0x3FEFD06B29DDF6DE,
        0x3C60F74E61E6C861, 0x3FEFC74518759BC8,
        0x3C90A3E45B33D399, 0x3FEFBE3ECAC6F383,
        0x3C979AA65D837B6D, 0x3FEFB5586CF9890F,
        0x3C8EB51A92FDEFFC, 0x3FEFAC922B7247F7,
        0x3C3EBE3D702F9CD1, 0x3FEFA3EC32D3D1A2,
        0xBC6A033489906E0B, 0x3FEF9B66AFFED31B,
        0xBC9556522A2FBD0E, 0x3FEF9301D0125B51,
        0xBC5080EF8C4EEA55, 0x3FEF8ABDC06C31CC,
        0xBC91C923B9D5F416, 0x3FEF829AAEA92DE0,
        0x3C80D3E3E95C55AF, 0x3FEF7A98C8A58E51,
        0xBC801B15EAA59348, 0x3FEF72B83C7D517B,
        0xBC8F1FF055DE323D, 0x3FEF6AF9388C8DEA,
        0x3C8B898C3F1353BF, 0x3FEF635BEB6FCB75,
        0xBC96D99C7611EB26, 0x3FEF5BE084045CD4,
        0x3C9AECF73E3A2F60, 0x3FEF54873168B9AA,
        0xBC8FE782CB86389D, 0x3FEF4D5022FCD91D,
        0x3C8A6F4144A6C38D, 0x3FEF463B88628CD6,
        0x3C807A05B0E4047D, 0x3FEF3F49917DDC96,
        0x3C968EFDE3A8A894, 0x3FEF387A6E756238,
        0x3C875E18F274487D, 0x3FEF31CE4FB2A63F,
        0x3C80472B981FE7F2, 0x3FEF2B4565E27CDD,
        0xBC96B87B3F71085E, 0x3FEF24DFE1F56381,
        0x3C82F7E16D09AB31, 0x3FEF1E9DF51FDEE1,
        0xBC3D219B1A6FBFFA, 0x3FEF187FD0DAD990,
        0x3C8B3782720C0AB4, 0x3FEF1285A6E4030B,
        0x3C6E149289CECB8F, 0x3FEF0CAFA93E2F56,
        0x3C834D754DB0ABB6, 0x3FEF06FE0A31B715,
        0x3C864201E2AC744C, 0x3FEF0170FC4CD831,
        0x3C8FDD395DD3F84A, 0x3FEEFC08B26416FF,
        0xBC86A3803B8E5B04, 0x3FEEF6C55F929FF1,
        0xBC924AEDCC4B5068, 0x3FEEF1A7373AA9CB,
        0xBC9907F81B512D8E, 0x3FEEECAE6D05D866,
        0xBC71D1E83E9436D2, 0x3FEEE7DB34E59FF7,
        0xBC991919B3CE1B15, 0x3FEEE32DC313A8E5,
        0x3C859F48A72A4C6D, 0x3FEEDEA64C123422,
        0xBC9312607A28698A, 0x3FEEDA4504AC801C,
        0xBC58A78F4817895B, 0x3FEED60A21F72E2A,
        0xBC7C2C9B67499A1B, 0x3FEED1F5D950A897,
        0x3C4363ED60C2AC11, 0x3FEECE086061892D,
        0x3C9666093B0664EF, 0x3FEECA41ED1D0057,
        0x3C6ECCE1DAA10379, 0x3FEEC6A2B5C13CD0,
        0x3C93FF8E3F0F1230, 0x3FEEC32AF0D7D3DE,
        0x3C7690CEBB7AAFB0, 0x3FEEBFDAD5362A27,
        0x3C931DBDEB54E077, 0x3FEEBCB299FDDD0D,
        0xBC8F94340071A38E, 0x3FEEB9B2769D2CA7,
        0xBC87DECCDC93A349, 0x3FEEB6DAA2CF6642,
        0xBC78DEC6BD0F385F, 0x3FEEB42B569D4F82,
        0xBC861246EC7B5CF6, 0x3FEEB1A4CA5D920F,
        0x3C93350518FDD78E, 0x3FEEAF4736B527DA,
        0x3C7B98B72F8A9B05, 0x3FEEAD12D497C7FD,
        0x3C9063E1E21C5409, 0x3FEEAB07DD485429,
        0x3C34C7855019C6EA, 0x3FEEA9268A5946B7,
        0x3C9432E62B64C035, 0x3FEEA76F15AD2148,
        0xBC8CE44A6199769F, 0x3FEEA5E1B976DC09,
        0xBC8C33C53BEF4DA8, 0x3FEEA47EB03A5585,
        0xBC845378892BE9AE, 0x3FEEA34634CCC320,
        0xBC93CEDD78565858, 0x3FEEA23882552225,
        0x3C5710AA807E1964, 0x3FEEA155D44CA973,
        0xBC93B3EFBF5E2228, 0x3FEEA09E667F3BCD,
        0xBC6A12AD8734B982, 0x3FEEA012750BDABF,
        0xBC6367EFB86DA9EE, 0x3FEE9FB23C651A2F,
        0xBC80DC3D54E08851, 0x3FEE9F7DF9519484,
        0xBC781F647E5A3ECF, 0x3FEE9F75E8EC5F74,
        0xBC86EE4AC08B7DB0, 0x3FEE9F9A48A58174,
        0xBC8619321E55E68A, 0x3FEE9FEB564267C9,
        0x3C909CCB5E09D4D3, 0x3FEEA0694FDE5D3F,
        0xBC7B32DCB94DA51D, 0x3FEEA11473EB0187,
        0x3C94ECFD5467C06B, 0x3FEEA1ED0130C132,
        0x3C65EBE1ABD66C55, 0x3FEEA2F336CF4E62,
        0xBC88A1C52FB3CF42, 0x3FEEA427543E1A12,
        0xBC9369B6F13B3734, 0x3FEEA589994CCE13,
        0xBC805E843A19FF1E, 0x3FEEA71A4623C7AD,
        0xBC94D450D872576E, 0x3FEEA8D99B4492ED,
        0x3C90AD675B0E8A00, 0x3FEEAAC7D98A6699,
        0x3C8DB72FC1F0EAB4, 0x3FEEACE5422AA0DB,
        0xBC65B6609CC5E7FF, 0x3FEEAF3216B5448C,
        0x3C7BF68359F35F44, 0x3FEEB1AE99157736,
        0xBC93091FA71E3D83, 0x3FEEB45B0B91FFC6,
        0xBC5DA9B88B6C1E29, 0x3FEEB737B0CDC5E5,
        0xBC6C23F97C90B959, 0x3FEEBA44CBC8520F,
        0xBC92434322F4F9AA, 0x3FEEBD829FDE4E50,
        0xBC85CA6CD7668E4B, 0x3FEEC0F170CA07BA,
        0x3C71AFFC2B91CE27, 0x3FEEC49182A3F090,
        0x3C6DD235E10A73BB, 0x3FEEC86319E32323,
        0xBC87C50422622263, 0x3FEECC667B5DE565,
        0x3C8B1C86E3E231D5, 0x3FEED09BEC4A2D33,
        0xBC91BBD1D3BCBB15, 0x3FEED503B23E255D,
        0x3C90CC319CEE31D2, 0x3FEED99E1330B358,
        0x3C8469846E735AB3, 0x3FEEDE6B5579FDBF,
        0xBC82DFCD978E9DB4, 0x3FEEE36BBFD3F37A,
        0x3C8C1A7792CB3387, 0x3FEEE89F995AD3AD,
        0xBC907B8F4AD1D9FA, 0x3FEEEE07298DB666,
        0xBC55C3D956DCAEBA, 0x3FEEF3A2B84F15FB,
        0xBC90A40E3DA6F640, 0x3FEEF9728DE5593A,
        0xBC68D6F438AD9334, 0x3FEEFF76F2FB5E47,
        0xBC91EEE26B588A35, 0x3FEF05B030A1064A,
        0x3C74FFD70A5FDDCD, 0x3FEF0C1E904BC1D2,
        0xBC91BDFBFA9298AC, 0x3FEF12C25BD71E09,
        0x3C736EAE30AF0CB3, 0x3FEF199BDD85529C,
        0x3C8EE3325C9FFD94, 0x3FEF20AB5FFFD07A,
        0x3C84E08FD10959AC, 0x3FEF27F12E57D14B,
        0x3C63CDAF384E1A67, 0x3FEF2F6D9406E7B5,
        0x3C676B2C6C921968, 0x3FEF3720DCEF9069,
        0xBC808A1883CCB5D2, 0x3FEF3F0B555DC3FA,
        0xBC8FAD5D3FFFFA6F, 0x3FEF472D4A07897C,
        0xBC900DAE3875A949, 0x3FEF4F87080D89F2,
        0x3C74A385A63D07A7, 0x3FEF5818DCFBA487,
        0xBC82919E2040220F, 0x3FEF60E316C98398,
        0x3C8E5A50D5C192AC, 0x3FEF69E603DB3285,
        0x3C843A59AC016B4B, 0x3FEF7321F301B460,
        0xBC82D52107B43E1F, 0x3FEF7C97337B9B5F,
        0xBC892AB93B470DC9, 0x3FEF864614F5A129,
        0x3C74B604603A88D3, 0x3FEF902EE78B3FF6,
        0x3C83C5EC519D7271, 0x3FEF9A51FBC74C83,
        0xBC8FF7128FD391F0, 0x3FEFA4AFA2A490DA,
        0xBC8DAE98E223747D, 0x3FEFAF482D8E67F1,
        0x3C8EC3BC41AA2008, 0x3FEFBA1BEE615A27,
        0x3C842B94C3A9EB32, 0x3FEFC52B376BBA97,
        0x3C8A64A931D185EE, 0x3FEFD0765B6E4540,
        0xBC8E37BAE43BE3ED, 0x3FEFDBFDAD9CBE14,
        0x3C77893B4D91CD9D, 0x3FEFE7C1819E90D8,
        0x3C5305C14160CC89, 0x3FEFF3C22B8F71F1,
    ];

    private const ulong Off = 0x3fe6955500000000;
    private const int SignBias = 0x800 << 7;
    private const ulong Unendlich = 0x7FF0000000000000;

    private static double D(ulong bits) => BitConverter.UInt64BitsToDouble(bits);

    private static ulong U(double d) => BitConverter.DoubleToUInt64Bits(d);

    private static uint Top12(double x) => (uint)(U(x) >> 52);

    private static double Fma(double a, double b, double c) => Math.FusedMultiplyAdd(a, b, c);

    // 0 = keine ganze Zahl, 1 = ungerade, 2 = gerade (iy endlich und nicht 0)
    private static int GanzeZahl(ulong iy)
    {
        int e = (int)(iy >> 52 & 0x7ff);
        if (e < 0x3ff)
        {
            return 0;
        }
        if (e > 0x3ff + 52)
        {
            return 2;
        }
        if ((iy & ((1UL << (0x3ff + 52 - e)) - 1)) != 0)
        {
            return 0;
        }
        return (iy & (1UL << (0x3ff + 52 - e))) != 0 ? 1 : 2;
    }

    // signalisierendes NaN (Ruhe-Bit 0)
    private static bool Signalisierend(ulong i) => 2 * (i ^ 0x0008000000000000) > 2 * 0x7FF8000000000000UL;

    private static bool NullUnendlichNan(ulong i) => 2 * i - 1 >= 2 * Unendlich - 1;

    private static double Ueberlauf(uint vorzeichen) => vorzeichen != 0 ? double.NegativeInfinity : double.PositiveInfinity;

    private static double Unterlauf(uint vorzeichen) => vorzeichen != 0 ? -0.0 : 0.0;

    /// <summary>Python: x ** y fuer Gleitkommazahlen (ohne Fehlerausloesung: ungueltig = NaN, Ueberlauf = Unendlich).</summary>
    public static double Pow(double x, double y)
    {
        uint vorzeichen = 0;
        ulong ix = U(x), iy = U(y);
        uint topx = Top12(x), topy = Top12(y);
        if (topx - 0x001 >= 0x7ff - 0x001 || (topy & 0x7ff) - 0x3be >= 0x43e - 0x3be)
        {
            if (NullUnendlichNan(iy))
            {
                if (2 * iy == 0)
                {
                    return Signalisierend(ix) ? x + y : 1.0;
                }
                if (ix == U(1.0))
                {
                    return Signalisierend(iy) ? x + y : 1.0;
                }
                if (2 * ix > 2 * Unendlich || 2 * iy > 2 * Unendlich)
                {
                    return x + y;
                }
                if (2 * ix == 2 * U(1.0))
                {
                    return 1.0;
                }
                if ((2 * ix < 2 * U(1.0)) == ((iy >> 63) == 0))
                {
                    return 0.0;
                }
                return y * y;
            }
            if (NullUnendlichNan(ix))
            {
                double x2 = x * x;
                if ((ix >> 63) != 0 && GanzeZahl(iy) == 1)
                {
                    x2 = -x2;
                }
                return (iy >> 63) != 0 ? 1 / x2 : x2;
            }
            if ((ix >> 63) != 0)
            {
                int ganz = GanzeZahl(iy);
                if (ganz == 0)
                {
                    return double.NaN;
                }
                if (ganz == 1)
                {
                    vorzeichen = SignBias;
                }
                ix &= 0x7fffffffffffffff;
                topx &= 0x7ff;
            }
            if ((topy & 0x7ff) - 0x3be >= 0x43e - 0x3be)
            {
                if (ix == U(1.0))
                {
                    return 1.0;
                }
                if ((topy & 0x7ff) < 0x3be)
                {
                    return ix > U(1.0) ? 1.0 + y : 1.0 - y;
                }
                return (ix > U(1.0)) == (topy < 0x800) ? Ueberlauf(0) : Unterlauf(0);
            }
            if (topx == 0)
            {
                ix = U(x * D(0x4330000000000000));
                ix &= 0x7fffffffffffffff;
                ix -= 52UL << 52;
            }
        }

        double hi = Log(ix, out double lo);
        double ehi = y * hi;
        double elo = Fma(y, lo, Fma(y, hi, -ehi));
        return Exp(ehi, elo, vorzeichen);
    }

    // log(x) = hi + tail mit etwa 15 Bit mehr Genauigkeit (pow.c: log_inline, FMA-Fassung)
    private static double Log(ulong ix, out double tail)
    {
        ulong tmp = ix - Off;
        int i = (int)((tmp >> 45) % 128);
        int k = (int)((long)tmp >> 52);
        ulong iz = ix - (tmp & 0xfffUL << 52);
        double z = D(iz);
        double kd = k;
        double invc = D(LogTab[3 * i]), logc = D(LogTab[3 * i + 1]), logctail = D(LogTab[3 * i + 2]);
        double r = Fma(z, invc, -1.0);
        double t1 = Fma(kd, D(Ln2Hi), logc);
        double t2 = r + t1;
        double lo1 = Fma(kd, D(Ln2Lo), logctail);
        double lo2 = r + (t1 - t2);
        double ar = r * D(LogPoly[0]);
        double ar2 = r * ar;
        double ar3 = r * ar2;
        double hi = t2 + ar2;
        double lo3 = Fma(r, ar, -ar2);
        double lo4 = ar2 + (t2 - hi);
        double p1 = Fma(r, D(LogPoly[2]), D(LogPoly[1]));
        double p3 = Fma(r, D(LogPoly[4]), D(LogPoly[3]));
        double p5 = Fma(r, D(LogPoly[6]), D(LogPoly[5]));
        double p = Fma(ar2, Fma(ar2, p5, p3), p1);
        double lo = Fma(ar3, p, lo4 + (lo3 + (lo1 + lo2)));
        double y = hi + lo;
        tail = lo + (hi - y);
        return y;
    }

    // Vorzeichen * exp(x + xtail) (pow.c: exp_inline und specialcase, FMA-Fassung)
    private static double Exp(double x, double xtail, uint vorzeichen)
    {
        uint abstop = Top12(x) & 0x7ff;
        if (abstop - Top12(D(0x3C90000000000000)) >= Top12(512.0) - Top12(D(0x3C90000000000000)))
        {
            if (abstop - Top12(D(0x3C90000000000000)) >= 0x80000000)
            {
                double eins = 1.0 + x;
                return vorzeichen != 0 ? -eins : eins;
            }
            if (abstop >= Top12(1024.0))
            {
                return (U(x) >> 63) != 0 ? Unterlauf(vorzeichen) : Ueberlauf(vorzeichen);
            }
            abstop = 0;
        }
        double z = Fma(x, D(InvLn2N), D(Shift));
        ulong ki = U(z);
        double kd = z - D(Shift);
        double r = Fma(kd, D(NegLn2LoN), Fma(kd, D(NegLn2HiN), x));
        r = xtail + r;
        ulong idx = 2 * (ki % 128);
        ulong top = (ki + vorzeichen) << 45;
        double tail = D(ExpTab[idx]);
        ulong sbits = ExpTab[idx + 1] + top;
        double r2 = r * r;
        double t = Fma(r2, Fma(r, D(ExpPoly[1]), D(ExpPoly[0])), r + tail);
        t = Fma(r2 * r2, Fma(r, D(ExpPoly[3]), D(ExpPoly[2])), t);
        if (abstop == 0)
        {
            return Sonderfall(t, sbits, ki);
        }
        double skala = D(sbits);
        return Fma(t, skala, skala);
    }

    // Ergebnis nahe Ueber- oder Unterlauf (pow.c: specialcase)
    private static double Sonderfall(double t, ulong sbits, ulong ki)
    {
        if ((ki & 0x80000000) == 0)
        {
            sbits -= 1009UL << 52;
            double s = D(sbits);
            return Fma(t, s, s) * D(0x7F00000000000000);
        }
        sbits += 1022UL << 52;
        double skala = D(sbits);
        double st = t * skala;
        double y = skala + st;
        if (Math.Abs(y) < 1.0)
        {
            double eins = y < 0.0 ? -1.0 : 1.0;
            double lo = st + (skala - y);
            double hi = y + eins;
            lo = lo + (y + (eins - hi));
            y = (hi + lo) - eins;
            if (y == 0.0)
            {
                y = D(sbits & 0x8000000000000000);
            }
        }
        return y * D(0x0010000000000000);
    }
}
