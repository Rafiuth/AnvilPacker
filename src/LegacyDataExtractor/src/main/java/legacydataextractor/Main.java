package legacydataextractor;

import com.google.common.base.*;
import com.google.common.collect.*;
import com.google.common.io.*;
import com.google.gson.*;
import net.minecraft.*;
import net.minecraft.block.*;
import net.minecraft.block.BlockDoor.*;
import net.minecraft.block.BlockRailBase.*;
import net.minecraft.block.material.*;
import net.minecraft.block.properties.*;
import net.minecraft.block.state.*;
import net.minecraft.block.state.BlockStateContainer.*;
import net.minecraft.init.*;
import net.minecraft.item.*;
import net.minecraft.util.*;
import net.minecraft.util.math.*;
import net.minecraft.util.registry.*;
import net.minecraft.world.*;
import net.minecraft.world.chunk.*;

import java.io.*;
import java.lang.reflect.*;
import java.util.*;
import java.util.Map.*;
import java.util.stream.*;

public class Main
{
    public static void main(String[] args) throws Throwable
    {
        System.out.println("Initializing Minecraft registries...");
        Bootstrap.register();

        System.out.println("Extracting data...");

        XData data = new XData();
        data.version = "1.12.2";
        data.worldVersion = 1343;
        data.numBlockStates = 0;

        for (Block block : Block.REGISTRY) {
            ResourceLocation key = Block.REGISTRY.getNameForObject(block);
            XBlock xb = new XBlock(key, block);
            data.blocks.add(xb);
            
            int maxStateId = (xb.id << 4) + 16;
            if (data.numBlockStates < maxStateId) {
                data.numBlockStates = maxStateId;
            }
        }

        String str = new Gson().toJson(data);

        Files.write(str, new File("legacy_blocks.json"), Charsets.UTF_8);

        System.out.println("Done");
    }

    static class XData
    {
        public String version;
        public int worldVersion;
        public int numBlockStates; //num of non existing block states based on metadata values
        public List<XBlock> blocks = new ArrayList<>();
        public Collection<XMaterial> materials = XMaterial.known.values();
    }
    static class XBlock
    {
        public String name;
        public int id;
        public int defaultStateId;
        public String material;
        public List<XBlockProperty> properties = new ArrayList<>();
        public XBlockStates states;

        public XBlock(ResourceLocation key, Block block)
        {
            name = key.getResourcePath();
            if (!key.getResourceDomain().equals("minecraft")) {
                name = key.toString();
            }
            
            id = Block.getIdFromBlock(block);
            
            states = new XBlockStates(block);
            
            for (IProperty<?> prop : block.getBlockState().getProperties()) {
                properties.add(XBlockProperty.create(prop));
            }
            IBlockState defaultState = block.getDefaultState();

            defaultStateId = Block.BLOCK_STATE_IDS.get(defaultState);

            material = XMaterial.known.computeIfAbsent(defaultState.getMaterial(), v -> {
                throw new IllegalStateException("Unknown material for block " + name);
            }).name;
        }
    }
    static class XBlockStates
    {
        public Object/* int|List<int> */ flags;
        public Object/* int|List<int> */ light;
        public Object/* string|List<String>*/ states;

        public XBlockStates(Block block)
        {
            List<Integer> flags = new ArrayList<>();
            List<Integer> light = new ArrayList<>();
            List<String> states = new ArrayList<>();
            
            //for (IBlockState state : block.getBlockState().getValidStates()) {
            int blockId = Block.getIdFromBlock(block);
            for (int m = 0; m < 16; m++) {
                IBlockState state = Block.BLOCK_STATE_IDS.getByValue(blockId << 4 | m);
                if (state == null) {
                    state = block.getDefaultState();
                    states.add(null);
                } else {
                    states.add(buildStateString(state));
                }
                flags.add(getFlags(state));
                light.add(state.getLightValue() << 4 | Math.min(15, state.getLightOpacity()));
            }
            this.flags = deduplicate(flags);
            this.light = deduplicate(light);
            
            if (block.getBlockState().getProperties().size() == 0) {
                states.clear();
            }
            this.states = deduplicate(states);
        }

        private String buildStateString(IBlockState state)
        {
            StringBuilder sb = new StringBuilder();
            for (Entry<IProperty<?>, Comparable<?>> kv : state.getProperties().entrySet()) {
                IProperty prop = kv.getKey();
                Comparable value = kv.getValue();
                
                if (sb.length() != 0) sb.append(',');
                sb.append(prop.getName(value));
            }
            return sb.toString();
        }

        private static <T> Object deduplicate(List<T> arr)
        {
            if (arr.stream().distinct().count() == 1) {
                return arr.get(0);
            }
            return arr;
        }

        private int getFlags(IBlockState bs)
        {
            int flags = 0;
            
            if (bs.getMaterial().isOpaque())
                flags |= 1 << 0;

            if (bs.isTranslucent())
                flags |= 1 << 1;

            if (bs.isFullCube())
                flags |= 1 << 2;

            if (bs.isOpaqueCube()) 
                flags |= 1 << 3;
            
            if (bs.getBlock().getTickRandomly())
                flags |= 1 << 4;

            if (bs.canProvidePower())
                flags |= 1 << 5;

            if (bs.getBlock() instanceof BlockLiquid) 
                flags |= 1 << 6;
            
            if (bs.getMaterial() == Material.AIR)
                flags |= 1 << 7;

            return flags;
        }
    }
    static class XMaterial
    {
        public static final Map<Material, XMaterial> known = new LinkedHashMap<>();

        public String name;
        public int attribs;
        public int mapColor;

        public XMaterial(Material material, String name)
        {
            this.name = name;

            attribs |= material.blocksMovement()    ? 1 << 0 : 0;
            attribs |= material.getCanBurn()        ? 1 << 1 : 0;
            attribs |= material.isLiquid()          ? 1 << 2 : 0;
            attribs |= material.blocksLight()       ? 1 << 3 : 0;
            attribs |= material.isReplaceable()     ? 1 << 4 : 0;
            attribs |= material.isSolid()           ? 1 << 5 : 0;

            mapColor = material.getMaterialMapColor().colorIndex;
        }

        private static void reg(Material mat, String name) { known.put(mat, new XMaterial(mat, name)); }

        static {
            reg(Material.AIR,           "air");
            reg(Material.GRASS,         "grass");
            reg(Material.GROUND,        "dirt");
            reg(Material.WOOD,          "wood");
            reg(Material.ROCK,          "stone");
            reg(Material.IRON,          "metal");
            reg(Material.ANVIL,         "repair_station");
            reg(Material.WATER,         "water");
            reg(Material.LAVA,          "lava");
            reg(Material.LEAVES,        "leaves");
            reg(Material.PLANTS,        "plant");
            reg(Material.VINE,          "replaceable_plant");
            reg(Material.SPONGE,        "sponge");
            reg(Material.CLOTH,         "wool");
            reg(Material.FIRE,          "fire");
            reg(Material.SAND,          "sand");
            reg(Material.CIRCUITS,      "decoration");
            reg(Material.CARPET,        "carpet");
            reg(Material.GLASS,         "glass");
            reg(Material.REDSTONE_LIGHT, "redstone_lamp");
            reg(Material.TNT,           "tnt");
            reg(Material.CORAL,         "coral");
            reg(Material.ICE,           "ice");
            reg(Material.PACKED_ICE,    "dense_ice");
            reg(Material.SNOW,          "snow_layer");
            reg(Material.CRAFTED_SNOW,  "snow_block");
            reg(Material.CACTUS,        "cactus");
            reg(Material.CLAY,          "clay");
            reg(Material.GOURD,         "vegetable");
            reg(Material.DRAGON_EGG,    "egg");
            reg(Material.PORTAL,        "portal");
            reg(Material.CAKE,          "cake");
            reg(Material.WEB,           "cobweb");
            reg(Material.PISTON,        "piston");
            reg(Material.BARRIER,       "barrier");
            reg(Material.STRUCTURE_VOID, "structural_air");
        }
    }


    static class XBlockProperty
    {
        public String name;
        public String type;

        public XBlockProperty(String name, String type)
        {
            this.name = name;
            this.type = type;
        }

        public static XBlockProperty create(IProperty<?> prop)
        {
            if (prop instanceof PropertyBool) {
                return new XPropBool((PropertyBool) prop);
            } else if (prop instanceof PropertyInteger) {
                return new XPropInt((PropertyInteger) prop);
            } else {
                return new XPropEnum((PropertyEnum<?>) prop);
            }
        }

        
        public static String getEnumName(Class<? extends Enum> type)
        {
            if (type == EnumFacing.class) return "Direction";
            if (type == BlockDirt.DirtType.class) return "DirtType";
            
            String prefix = trimPrefix(type.getEnclosingClass() == null ? "" : type.getEnclosingClass().getSimpleName(), "Block");
            String name = trimPrefix(type.getSimpleName(), "Enum");
            
            return prefix + name;
        }
        private static String trimPrefix(String str, String prefix)
        {
            return str.startsWith(prefix) ? str.substring(prefix.length()) : str;
        }

        public static class XPropInt extends XBlockProperty
        {
            public int min, max;

            public XPropInt(PropertyInteger prop)
            {
                super(prop.getName(), "int");

                min = prop.getAllowedValues().stream().min(Integer::compareTo).get();
                max = prop.getAllowedValues().stream().max(Integer::compareTo).get();
            }
        }

        public static class XPropBool extends XBlockProperty
        {
            public XPropBool(PropertyBool prop)
            {
                super(prop.getName(), "bool");
            }
        }

        public static class XPropEnum extends XBlockProperty
        {
            public String enumType;
            public List<String> values;

            public XPropEnum(PropertyEnum<? extends Enum<?>> prop)
            {
                super(prop.getName(), "enum");
                this.enumType = getEnumName(prop.getValueClass());
                this.values = prop.getAllowedValues()
                                  .stream()
                                  .map(v -> v.getName())
                                  .collect(Collectors.toList());
            }
        }
    }
}