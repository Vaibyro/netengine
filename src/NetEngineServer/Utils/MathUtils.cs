namespace NetEngineServer.Utils {
    public static class MathUtils {
        public static long Clamp(long val, long min, long max = 0) {
            if (val < min) return min;
            else if (max != 0 && val > max) return max;
            else return val;
        }
        
        public static int Clamp(int val, int min, int max = 0) {
            if (val < min) return min;
            else if (max != 0 && val > max) return max;
            else return val;
        }
        
        public static byte Clamp(byte val, byte min, byte max = 0) {
            if (val < min) return min;
            else if (max != 0 && val > max) return max;
            else return val;
        }
    }
}