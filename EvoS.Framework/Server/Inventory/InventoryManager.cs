using System;
using System.Collections.Generic;
using EvoS.Framework.Network.Static;

namespace EvoS.DirectoryServer.Inventory
{
    public class InventoryManager
    {
        public static List<int> GetUnlockedBannerIDs(long accountId)
        {
            return new List<int>() { 285, 375, 289, 290, 291, 292, 293, 294, 108, 110, 112, 114, 259, 275, 395, 418, 420, 424, 443, 470, 288, 38, 61, 62, 62, 63, 63, 64, 64, 65, 65, 67, 67, 68, 68, 69, 69, 70, 70, 73, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 109, 111, 113, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142, 142, 143, 143, 144, 144, 145, 145, 146, 146, 147, 147, 148, 148, 149, 149, 150, 150, 151, 151, 152, 152, 153, 153, 154, 154, 155, 155, 156, 156, 157, 157, 158, 158, 159, 159, 160, 160, 161, 161, 162, 162, 163, 163, 164, 164, 165, 165, 166, 166, 167, 167, 168, 168, 169, 169, 170, 171, 173, 175, 177, 179, 181, 183, 185, 186, 187, 187, 188, 189, 189, 190, 191, 192, 193, 194, 195, 196, 197, 197, 198, 199, 199, 200, 201, 201, 201, 202, 203, 203, 203, 204, 205, 205, 206, 207, 207, 208, 209, 209, 210, 211, 211, 212, 213, 213, 214, 215, 215, 216, 217, 217, 218, 219, 220, 221, 222, 222, 222, 223, 224, 224, 225, 226, 226, 227, 227, 228, 229, 229, 230, 231, 232, 233, 234, 235, 236, 237, 238, 239, 239, 240, 240, 241, 241, 242, 243, 244, 245, 245, 246, 246, 247, 247, 248, 249, 250, 251, 251, 252, 252, 253, 253, 254, 255, 256, 256, 257, 257, 258, 260, 261, 261, 262, 263, 263, 264, 265, 265, 266, 267, 267, 268, 269, 270, 271, 272, 273, 274, 276, 277, 278, 278, 279, 280, 280, 281, 282, 282, 283, 284, 284, 286, 295, 296, 296, 297, 298, 298, 299, 299, 300, 300, 301, 301, 302, 302, 303, 303, 304, 304, 305, 305, 306, 306, 307, 307, 308, 308, 309, 309, 310, 310, 311, 311, 312, 312, 313, 313, 314, 314, 315, 315, 316, 316, 317, 317, 318, 318, 319, 319, 320, 320, 321, 321, 322, 322, 323, 323, 324, 324, 325, 325, 326, 326, 327, 327, 328, 328, 329, 330, 331, 331, 332, 332, 333, 333, 334, 334, 335, 335, 336, 337, 338, 339, 340, 341, 342, 343, 345, 347, 350, 351, 352, 353, 354, 355, 356, 356, 357, 358, 359, 360, 361, 361, 362, 362, 363, 363, 364, 364, 365, 365, 366, 366, 367, 367, 368, 369, 369, 370, 371, 372, 373, 373, 374, 374, 375, 376, 376, 377, 377, 378, 378, 379, 379, 379, 380, 380, 381, 381, 381, 382, 382, 383, 384, 385, 386, 386, 387, 387, 388, 388, 389, 389, 390, 390, 391, 391, 392, 393, 394, 396, 397, 398, 399, 400, 401, 402, 403, 404, 405, 406, 407, 408, 409, 410, 411, 412, 413, 414, 415, 415, 416, 416, 417, 419, 421, 422, 423, 423, 424, 425, 426, 426, 427, 427, 428, 429, 430, 430, 431, 431, 432, 433, 434, 434, 435, 435, 436, 436, 437, 437, 438, 438, 439, 439, 440, 440, 441, 442, 442, 443, 444, 444, 445, 446, 446, 452, 452, 453, 453, 454, 455, 456, 456, 457, 458, 458, 459, 459, 460, 460, 461, 461, 462, 462, 463, 464, 465, 465, 466, 466, 467, 467, 467, 468, 469, 469, 471, 471, 472, 473, 473, 474, 475, 475, 476, 476, 477, 477, 478, 478, 479, 479, 480, 480, 481, 481, 482, 482 };
        }

        public static List<int> GetUnlockedEmojiIDs(long accountId)
        {
            return new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 25, 21, 26, 27, 31, 30, 28, 29, 32, 33, 34, 35, 38, 37, 36, 39, 22, 23, 24, 41, 42, 43, 44, 45 };
        }

        public static List<int> GetUnlockedLoadingScreenBackgroundIds(long accountId)
        {
            return new List<int>() { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 1 };
        }

        public static Dictionary<int, bool> GetActivatedLoadingScreenBackgroundIds(long accountId)
        {
            Dictionary<int, bool> backgrounds = new Dictionary<int, bool>();

            for (int i = 1; i <= 18; i++)
            {
                backgrounds.Add(i, true);
            }

            return backgrounds;
        }

        public static List<int> GetDefaultUnlockedBannerIDs(long accountId)
        {
            return new List<int>() { 65, 95, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 217, 222, 236, 237, 238, 239, 240, 241, 242, 243, 244, 245, 246, 247, 248, 249, 250, 251, 252, 253, 286, 324, 325, 326, 327, 328, 333, 334, 335, 336, 337, 338, 339, 340, 341, 342, 343, 345, 347, 350, 351, 352, 353, 354, 355, 356, 357, 358, 359, 360, 386, 387, 388, 389, 390, 391, 392, 393, 398, 399, 400, 401, 402, 403, 404, 405, 406, 407, 408, 409, 410, 411, 412, 423, 424, 434, 441, 442, 443, 457, 467, 468 };
        }

        public static List<int> GetUnlockedOverconIDs(long accountId)
        {
            return new List<int>() { 4, 1, 2, 9, 10, 5, 3, 7, 8, 6, 12, 13, 11, 19, 20, 5, 16, 17, 15, 18, 14, 21, 24, 25, 26, 27, 28, 29, 29, 39, 37, 38, 41, 40, 43, 42, 44, 45, 46, 47, 30, 31, 32, 33, 34 };
        }

        public static List<int> GetUnlockedTitleIDs(long accountId)
        {
            //TODO
            return new List<int>() { 5, 6, 7, 8, 9, 10, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 27, 28, 29, 30, 31, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 43, 44, 44, 45, 45, 46, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 77, 78, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 91, 92, 93, 94, 95, 96, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 149 };
        }

        public static List<int> GetUnlockedRibbonIDs(long accountId)
        {
            //TODO
            return new List<int>();
        }

        public static InventoryComponent GetInventoryComponent(long accountId)
        {
            // TODO
            return new InventoryComponent();
        }

        public static Boolean BannerIsForeground(int bannerID)
        {
            List<int> fg = new List<int>{ 62, 63, 64, 65, 67, 68, 69, 70, 73, 96, 109, 111, 113, 115, 142, 143, 144, 145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 173, 175, 177, 179, 181, 183, 185, 187, 189, 190, 191, 192, 193, 194, 195, 197, 199, 201, 201, 203, 203, 205, 207, 209, 211, 213, 215, 216, 217, 218, 219, 220, 221, 222, 224, 226, 227, 229, 231, 232, 233, 234, 235, 239, 240, 241, 245, 246, 247, 251, 252, 253, 256, 257, 258, 261, 263, 265, 267, 268, 269, 270, 271, 272, 273, 274, 276, 278, 280, 282, 284, 296, 298, 299, 300, 301, 302, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313, 314, 315, 316, 317, 318, 319, 320, 321, 322, 323, 324, 325, 326, 327, 328, 331, 332, 333, 334, 335, 356, 361, 362, 363, 364, 365, 366, 367, 369, 373, 374, 375, 376, 377, 378, 379, 381, 383, 384, 385, 386, 387, 388, 389, 390, 391, 394, 396, 397, 415, 416, 417, 419, 421, 422, 423, 425, 426, 427, 430, 431, 434, 435, 436, 437, 438, 439, 440, 442, 444, 445, 446, 452, 453, 456, 458, 459, 460, 461, 462, 465, 466, 467, 467, 469, 469, 471, 473, 475, 476, 477, 478, 479, 480, 481, 482 };
            return fg.Contains(bannerID);
        }

        internal class VfxCost
        {
            public int VfxId { get; set; }
            public int AbilityId { get; set; }
            public int Cost { get; set; }
        }

        internal class BannerCost
        {
            public int Id { get; set; }
            public int Cost { get; set; }
        }

        public static int GetVfxCost(int vfxId, int AbilityId)
        {
            List<VfxCost> vfxList = new List<VfxCost>
            {
                // BattleMonk
                new VfxCost { VfxId = 200, AbilityId = 0, Cost = 5000 },
                // BazookaGirl
                new VfxCost { VfxId = 300, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 300, AbilityId = 2, Cost = 1500 },
                // DigitalSorceress
                new VfxCost { VfxId = 400, AbilityId = 1, Cost = 5000 },
                new VfxCost { VfxId = 400, AbilityId = 2, Cost = 1200 },
                new VfxCost { VfxId = 400, AbilityId = 3, Cost = 1200 },
                new VfxCost { VfxId = 400, AbilityId = 4, Cost = 1500 },
                new VfxCost { VfxId = 401, AbilityId = 0, Cost = 1200 },
                // Gremlins
                new VfxCost { VfxId = 300, AbilityId = 0, Cost = 5000 },
                // NanoSmith
                new VfxCost { VfxId = 600, AbilityId = 3, Cost = 1500 },
                new VfxCost { VfxId = 600, AbilityId = 0, Cost = 5000 },
                // RageBeast 
                new VfxCost { VfxId = 700, AbilityId = 0, Cost = 5000 },
                // RobotAnimal
                new VfxCost { VfxId = 800, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 800, AbilityId = 4, Cost = 1500 },
                // Scoundrel
                new VfxCost { VfxId = 900, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 900, AbilityId = 1, Cost = 1500 },
                // Sniper
                new VfxCost { VfxId = 1000, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 1000, AbilityId = 4, Cost = 1500 },
                // SpaceMarine
                new VfxCost { VfxId = 1100, AbilityId = 0, Cost = 5000 },
                // Spark
                new VfxCost { VfxId = 1200, AbilityId = 0, Cost = 5000 },
                // TeleportingNinja
                new VfxCost { VfxId = 1300, AbilityId = 0, Cost = 5000 },
                // Thief
                new VfxCost { VfxId = 1400, AbilityId = 0, Cost = 5000 },
                // Tracker
                new VfxCost { VfxId = 1500, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 1500, AbilityId = 1, Cost = 1200 },
                // Trickster
                new VfxCost { VfxId = 1600, AbilityId = 0, Cost = 1500 },
                new VfxCost { VfxId = 1600, AbilityId = 1, Cost = 1200 },
                // Rampart
                new VfxCost { VfxId = 1800, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 1801, AbilityId = 3, Cost = 1200 },
                // Claymore
                new VfxCost { VfxId = 1900, AbilityId = 0, Cost = 5000 },
                // Blaster
                new VfxCost { VfxId = 2000, AbilityId = 0, Cost = 5000 },
                // FishMan
                new VfxCost { VfxId = 2100, AbilityId = 0, Cost = 5000 },
                // Exo
                new VfxCost { VfxId = 2200, AbilityId = 0, Cost = 5000 },
                // Soldier (id 2301 ability 4 unable to get normaly)
                new VfxCost { VfxId = 2300, AbilityId = 0, Cost = 5000 },
                // Martyr
                new VfxCost { VfxId = 2400, AbilityId = 0, Cost = 5000 },
                // Sensei
                new VfxCost { VfxId = 2500, AbilityId = 0, Cost = 5000 },
                // Manta
                new VfxCost { VfxId = 2700, AbilityId = 0, Cost = 5000 },
                // Valkyrie
                new VfxCost { VfxId = 2800, AbilityId = 0, Cost = 5000 },
                // Archer
                new VfxCost { VfxId = 2900, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 2900, AbilityId = 0, Cost = 1200 },
                // Samurai
                new VfxCost { VfxId = 3200, AbilityId = 0, Cost = 5000 },
                // Cleric
                new VfxCost { VfxId = 3400, AbilityId = 0, Cost = 5000 },
                // Neko
                new VfxCost { VfxId = 3500, AbilityId = 0, Cost = 5000 },
                // Scamp
                new VfxCost { VfxId = 3600, AbilityId = 0, Cost = 5000 },
                // Dino
                new VfxCost { VfxId = 3500, AbilityId = 0, Cost = 5000 },
                // Iceborg
                new VfxCost { VfxId = 3900, AbilityId = 0, Cost = 5000 },
                // Fireborg
                new VfxCost { VfxId = 4000, AbilityId = 0, Cost = 5000 }
            };
            VfxCost result = vfxList.Find(m => (m.AbilityId == AbilityId) && (m.VfxId == vfxId));
            return result.Cost;
        }

        public static int GetBannerCost(int Id)
        {
            List<BannerCost> List = new List<BannerCost>
            {
                new BannerCost { Id = 368, Cost = 1500 },
                new BannerCost { Id = 370, Cost = 1500 },
                new BannerCost { Id = 288, Cost = 25000 },
                new BannerCost { Id = 376, Cost = 300 },
                new BannerCost { Id = 377, Cost = 300 },
                new BannerCost { Id = 378, Cost = 300 },
                new BannerCost { Id = 383, Cost = 25000 },
                new BannerCost { Id = 384, Cost = 25000 },
                new BannerCost { Id = 385, Cost = 25000 },
                new BannerCost { Id = 435, Cost = 500 },
                new BannerCost { Id = 436, Cost = 500 },
                new BannerCost { Id = 437, Cost = 500 },
                new BannerCost { Id = 438, Cost = 500 },
                new BannerCost { Id = 439, Cost = 500 },
                new BannerCost { Id = 440, Cost = 500 },
                new BannerCost { Id = 299, Cost = 300 },
                new BannerCost { Id = 300, Cost = 300 },
                new BannerCost { Id = 301, Cost = 300 },
                new BannerCost { Id = 302, Cost = 300 },
                new BannerCost { Id = 303, Cost = 300 },
                new BannerCost { Id = 304, Cost = 300 },
                new BannerCost { Id = 305, Cost = 300 },
                new BannerCost { Id = 306, Cost = 300 },
                new BannerCost { Id = 307, Cost = 300 },
                new BannerCost { Id = 308, Cost = 300 },
                new BannerCost { Id = 309, Cost = 300 },
                new BannerCost { Id = 310, Cost = 300 },
                new BannerCost { Id = 311, Cost = 300 },
                new BannerCost { Id = 312, Cost = 300 },
                new BannerCost { Id = 313, Cost = 300 },
                new BannerCost { Id = 314, Cost = 300 },
                new BannerCost { Id = 315, Cost = 300 },
                new BannerCost { Id = 316, Cost = 300 },
                new BannerCost { Id = 317, Cost = 300 },
                new BannerCost { Id = 318, Cost = 300 },
                new BannerCost { Id = 319, Cost = 300 },
                new BannerCost { Id = 320, Cost = 300 },
                new BannerCost { Id = 321, Cost = 300 },
                new BannerCost { Id = 322, Cost = 300 },
                new BannerCost { Id = 323, Cost = 300 },
                new BannerCost { Id = 361, Cost = 300 },
                new BannerCost { Id = 362, Cost = 300 },
                new BannerCost { Id = 363, Cost = 300 },
                new BannerCost { Id = 364, Cost = 300 },
                new BannerCost { Id = 365, Cost = 300 },
                new BannerCost { Id = 366, Cost = 300 },
                new BannerCost { Id = 367, Cost = 1500 },
                new BannerCost { Id = 369, Cost = 1500 }
            };
            BannerCost result = List.Find(m => m.Id == Id);
            return result != null ? result.Cost : 100; // this way dont have to add crazy amount to the list so defaults 100
        }
    }
}
