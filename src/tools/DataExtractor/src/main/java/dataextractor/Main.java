package dataextractor;

import com.google.common.base.*;
import com.google.common.collect.*;
import com.google.common.io.*;
import com.google.gson.*;
import com.mojang.bridge.game.*;
import net.minecraft.*;
import net.minecraft.block.*;
import net.minecraft.state.*;
import net.minecraft.state.property.*;
import net.minecraft.util.*;
import net.minecraft.util.math.*;
import net.minecraft.util.registry.*;
import net.minecraft.world.*;

import java.io.*;
import java.lang.reflect.*;
import java.util.*;
import java.util.stream.*;

public class Main
{
    public static void main(String[] args) throws Throwable
    {
        GameVersion version = MinecraftVersion.create();
        System.out.println("Initializing Minecraft " + version.getName() + " registries...");
        Bootstrap.initialize();

        System.out.println("Extracting data...");

        XData data = new XData();
        data.version = version.getName();
        data.worldVersion = version.getWorldVersion();
        data.numBlockStates = 0;

        for (Block block : Registry.BLOCK) {
            Identifier key = Registry.BLOCK.getId(block);
            data.blocks.add(new XBlock(key, block));
            data.numBlockStates += block.getStateManager().getStates().size();
        }

        String str = new Gson().toJson(data);

        Files.write(str, new File("blocks.json"), Charsets.UTF_8);

        System.out.println("Done");
    }

    static class XData
    {
        public String version;
        public int worldVersion;
        public int numBlockStates;
        public List<XBlock> blocks = new ArrayList<>();
        public Collection<XMaterial> materials = XMaterial.known.values();
    }
    static class XBlock
    {
        public String name;
        public int minStateId, maxStateId;
        public int defaultStateId;
        public String material;
        public List<XBlockProperty> properties = new ArrayList<>();
        public XBlockStates states;

        public XBlock(Identifier key, Block block)
        {
            name = key.getPath();
            if (!key.getNamespace().equals("minecraft")) {
                name = key.toString();
            }
            StateManager<Block, BlockState> stateMgr = block.getStateManager();
            
            minStateId = Integer.MAX_VALUE;
            maxStateId = 0;
            for (BlockState bs : stateMgr.getStates()) {
                int id = Block.getRawIdFromState(bs);
                minStateId = Math.min(minStateId, id);
                maxStateId = Math.max(maxStateId, id);
            }
            
            states = new XBlockStates(block);

            for (Property<?> prop : block.getStateManager().getProperties()) {
                properties.add(XBlockProperty.create(prop));
            }
            BlockState defaultState = block.getDefaultState();

            defaultStateId = Block.getRawIdFromState(defaultState);

            material = XMaterial.known.computeIfAbsent(defaultState.getMaterial(), v -> {
                throw new IllegalStateException("Unknown material for block " + name);
            }).name;
        }
    }
    static class XBlockStates
    {
        public Object/* int|List<int> */ flags;
        public Object/* int|List<int> */ light;

        private static final BlockView emptyView = EmptyBlockView.INSTANCE;
        private static final BlockPos zeroPos = BlockPos.ORIGIN;

        public XBlockStates(Block block)
        {
            StateManager<Block, BlockState> stateMgr = block.getStateManager();
            List<Integer> flags = new ArrayList<>();
            List<Integer> light = new ArrayList<>();

            for (BlockState state : stateMgr.getStates()) {
                flags.add(getFlags(state));
                light.add(state.getLuminance() << 4 | state.getOpacity(emptyView, zeroPos));
            }
            this.flags = deduplicate(flags);
            this.light = deduplicate(light);
        }

        private static Object deduplicate(List<Integer> arr)
        {
            if (arr.stream().distinct().count() == 1) {
                return arr.get(0);
            }
            return arr;
        }

        private int getFlags(BlockState bs)
        {
            int flags = 0;

            if (bs.isOpaque())
                flags |= 1 << 0;

            if (bs.isTranslucent(emptyView, zeroPos))
                flags |= 1 << 1;

            if (bs.isFullCube(emptyView, zeroPos))
                flags |= 1 << 2;

            if (bs.isOpaqueFullCube(emptyView, zeroPos))
                flags |= 1 << 3; //Opaque && FullCube

            if (bs.hasRandomTicks())
                flags |= 1 << 4;

            if (bs.emitsRedstonePower())
                flags |= 1 << 5;

            if (!bs.getFluidState().isEmpty())
                flags |= 1 << 6; //IsImmerse

            if (bs.isAir())
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
            attribs |= material.isBurnable()        ? 1 << 1 : 0;
            attribs |= material.isLiquid()          ? 1 << 2 : 0;
            attribs |= material.blocksLight()       ? 1 << 3 : 0;
            attribs |= material.isReplaceable()     ? 1 << 4 : 0;
            attribs |= material.isSolid()           ? 1 << 5 : 0;

            mapColor = material.getColor().id;
        }

        private static void reg(Material mat, String name) { known.put(mat, new XMaterial(mat, name)); }

        static {
            reg(Material.AIR                , "air");
            reg(Material.STRUCTURE_VOID     , "structural_air");
            reg(Material.PORTAL             , "portal");
            reg(Material.CARPET             , "carpet");
            reg(Material.PLANT              , "plant");
            reg(Material.UNDERWATER_PLANT   , "water_plant");
            reg(Material.REPLACEABLE_PLANT  , "replaceable_plant");
            reg(Material.NETHER_SHOOTS      , "replaceable_fireproof_plant");
            reg(Material.REPLACEABLE_UNDERWATER_PLANT, "replaceable_water_plant");
            reg(Material.WATER              , "water");
            reg(Material.BUBBLE_COLUMN      , "bubble_column");
            reg(Material.LAVA               , "lava");
            reg(Material.SNOW_LAYER         , "snow_layer");
            reg(Material.FIRE               , "fire");
            reg(Material.SUPPORTED          , "decoration");
            reg(Material.COBWEB             , "cobweb");
            reg(Material.REDSTONE_LAMP      , "redstone_lamp");
            reg(Material.ORGANIC_PRODUCT    , "clay");
            reg(Material.SOIL               , "dirt");
            reg(Material.SOLID_ORGANIC      , "grass");
            reg(Material.DENSE_ICE          , "dense_ice");
            reg(Material.AGGREGATE          , "sand");
            reg(Material.SPONGE             , "sponge");
            reg(Material.SHULKER_BOX        , "shulker_box");
            reg(Material.WOOD               , "wood");
            reg(Material.NETHER_WOOD        , "nether_wood");
            reg(Material.BAMBOO_SAPLING     , "bamboo_sapling");
            reg(Material.BAMBOO             , "bamboo");
            reg(Material.WOOL               , "wool");
            reg(Material.TNT                , "tnt");
            reg(Material.LEAVES             , "leaves");
            reg(Material.GLASS              , "glass");
            reg(Material.ICE                , "ice");
            reg(Material.CACTUS             , "cactus");
            reg(Material.STONE              , "stone");
            reg(Material.METAL              , "metal");
            reg(Material.SNOW_BLOCK         , "snow_block");
            reg(Material.REPAIR_STATION     , "repair_station");
            reg(Material.BARRIER            , "barrier");
            reg(Material.PISTON             , "piston");
            reg(Material.UNUSED_PLANT       , "coral");
            reg(Material.GOURD              , "vegetable");
            reg(Material.EGG                , "egg");
            reg(Material.CAKE               , "cake");
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

        public static XBlockProperty create(Property<?> prop)
        {
            if (prop instanceof BooleanProperty) {
                return new XPropBool((BooleanProperty) prop);
            } else if (prop instanceof IntProperty) {
                return new XPropInt((IntProperty) prop);
            } else {
                return new XPropEnum((EnumProperty<?>) prop);
            }
        }

        public static String getEnumName(Class<? extends Enum> type)
        {
            return type.getSimpleName();
        }

        public static class XPropInt extends XBlockProperty
        {
            public int min, max;

            public XPropInt(IntProperty prop)
            {
                super(prop.getName(), "int");

                min = prop.getValues().stream().min(Integer::compareTo).get();
                max = prop.getValues().stream().max(Integer::compareTo).get();
            }
        }

        public static class XPropBool extends XBlockProperty
        {
            public XPropBool(BooleanProperty prop)
            {
                super(prop.getName(), "bool");
            }
        }

        public static class XPropEnum extends XBlockProperty
        {
            public String enumType;
            public List<String> values;

            public XPropEnum(EnumProperty<? extends Enum<?>> prop)
            {
                super(prop.getName(), "enum");
                this.enumType = getEnumName(prop.getType());
                this.values = prop.getValues()
                                  .stream()
                                  .map(v -> v.asString())
                                  .collect(Collectors.toList());
            }
        }
    }

}