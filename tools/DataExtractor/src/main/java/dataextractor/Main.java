package dataextractor;

import com.google.common.base.*;
import com.google.common.collect.*;
import com.google.common.io.*;
import com.google.gson.*;
import com.google.gson.reflect.*;
import com.google.gson.stream.*;
import com.mojang.bridge.game.*;
import net.minecraft.*;
import net.minecraft.block.*;
import net.minecraft.state.property.*;
import net.minecraft.util.*;
import net.minecraft.util.math.*;
import net.minecraft.util.registry.*;
import net.minecraft.util.shape.*;
import net.minecraft.world.*;

import java.io.*;
import java.util.*;
import java.util.Objects;
import java.util.regex.*;
import java.util.stream.*;

public class Main
{
    private static XData data = new XData();
    
    public static void main(String[] args) throws Throwable
    {
        SharedConstants.createGameVersion();
        GameVersion version = SharedConstants.getGameVersion();
        System.out.println("Initializing Minecraft " + version.getName() + " registries...");
        Bootstrap.initialize();

        System.out.println("Extracting data...");

        data.version = version.getName();
        data.worldVersion = version.getWorldVersion();
        data.numBlockStates = 0;

        var blocks = new LinkedHashMap<XBlock, XBlock>();
        
        for (Block block : Registry.BLOCK) {
            Identifier key = Registry.BLOCK.getId(block);
            var xblock = new XBlock(key, block, data);
            blocks.compute(xblock, (k, prev) -> {
                if (prev == null) return k;
                prev.names.add(xblock.names.get(0));
                return prev; 
            });
            data.numBlockStates += xblock.numStates;
        }

        data.shapes = data.shapeCache.keySet().stream().map(boxes -> {
            return boxes.stream().flatMapToInt(bb -> {
                return IntStream.of(
                    (int)Math.round(bb.minX * 16), 
                    (int)Math.round(bb.minY * 16), 
                    (int)Math.round(bb.minZ * 16),
                    (int)Math.round(bb.maxX * 16), 
                    (int)Math.round(bb.maxY * 16), 
                    (int)Math.round(bb.maxZ * 16)
                );
            }).toArray();
        }).collect(Collectors.toList());

        data.blocks.addAll(blocks.keySet());
        
        var gson = new GsonBuilder()
            .setPrettyPrinting()
            .disableHtmlEscaping()
            .create();
        String json = gson.toJson(data);
        
        //Note: this shitty regex will crash the shitty java regex engine with a stackoverflow.
        //Launch with -Xss64m as an workaround
        //int arrays \[\s*(?:-?\d+\s*,\s*)*\s*-?\d+\s*\]
        json = minify(json, "\\[\\s*(?:-?\\d+\\s*,\\s*)*\\s*-?\\d+\\s*\\]");
        //string arrays \[\s*(?:\"[A-Za-z0-9 ,_\-:$]*\"\s*,\s*)*\s*\"[A-Za-z0-9 ,_\-:$]*\"\s*\]
        json = minify(json, "\\[\\s*(?:\\\"[A-Za-z0-9 ,_\\-:$]*\\\"\\s*,\\s*)*\\s*\\\"[A-Za-z0-9 ,_\\-:$]*\\\"\\s*\\]");

        Files.write(json, new File("blocks.json"), Charsets.UTF_8);

        System.out.println("Done");
    }

    static String minify(String json, String regex)
    {
        var pattern = Pattern.compile(regex);
        var matcher = pattern.matcher(json);
        return matcher.replaceAll(m -> {
            String s = m.group(0);
            s = s.replaceAll("[\\r\\n\\s]+", "").replace(",", ", ");
            return Matcher.quoteReplacement(s);
        });
    }

    static class XData
    {
        public String version;
        public int worldVersion;
        public int numBlockStates;

        public List<XBlock> blocks = new ArrayList<>();
        public Collection<XMaterial> materials = XMaterial.known.values();

        public transient Map<List<Box>, Integer> shapeCache = new LinkedHashMap<>();
        public List<int[]> shapes;

        public int getShapeId(VoxelShape shape)
        {
            var bbs = shape.getBoundingBoxes();
            return shapeCache.computeIfAbsent(bbs, k -> shapeCache.size());
        }
    }
    static class XBlock
    {
        public List<String> names = new ArrayList<>();
        public int numStates;
        public int defaultStateId;
        public String material;
        public List<XBlockProperty> properties = new ArrayList<>();
        public XBlockStates states;

        public XBlock(Identifier key, Block block, XData data)
        {
            var name = key.getPath();
            if (!key.getNamespace().equals("minecraft")) {
                name = key.toString();
            }
            names.add(name);

            var stateMgr = block.getStateManager();
            var sortedStates = ImmutableList.sortedCopyOf(
                Comparator.comparingInt(XBlock::getStateIndex), 
                stateMgr.getStates()
            );

            numStates = sortedStates.size();
            states = new XBlockStates(block, sortedStates);
            var defaultState = block.getDefaultState();

            defaultStateId = getStateIndex(defaultState);

            for (Property<?> prop : block.getStateManager().getProperties()) {
                properties.add(XBlockProperty.create(prop));
            }

            material = XMaterial.known.computeIfAbsent(defaultState.getMaterial(), v -> {
                throw new IllegalStateException("Unknown material for block " + key);
            }).name;
        }

        public static int getStateIndex(BlockState state)
        {
            int id = 0;
            int shift = 1;

            var props = state.getBlock().getStateManager().getProperties();

            for (Property<?> prop : props) {
                id += getValueIndex(state, prop) * shift;
                shift *= prop.getValues().size();
            }
            return id;
        }
        private static int getValueIndex(BlockState state, Property<?> prop)
        {
            int index = 0;

            Object value = state.get(prop);
            for (Object possValue : prop.getValues()) {
                if (value.equals(possValue)) {
                    return index;
                }
                index++;
            }
            throw new IllegalStateException("Block value index not found: " + state.toString() + " " + prop.getName());
        }

        //Java records are so bad -
        //you can't have hierarchies, no extra ctors, THE BRACES ARE REQUIRED
        //like wtf, i'd rather use lombok
        @Override
        public boolean equals(Object obj)
        {
            return obj instanceof XBlock b && 
                   b.defaultStateId == defaultStateId &&
                   b.numStates == numStates && 
                   b.material.equals(material) &&
                   b.properties.equals(properties) &&
                   b.states.equals(states);
        }
        @Override
        public int hashCode()
        {
            return properties.hashCode();
        }
    }
    static class XBlockStates
    {
        public Object/* int|List<int> */ flags;
        public Object/* int|List<int> */ light;
        public Object/* int|List<int> */ occlusionShapes;

        private static final BlockView emptyView = EmptyBlockView.INSTANCE;
        private static final BlockPos zeroPos = BlockPos.ORIGIN;

        public XBlockStates(Block block, List<BlockState> states)
        {
            var flags = new ArrayList<Integer>();
            var light = new ArrayList<Integer>();
            var occlusionShapes = new ArrayList<Integer>();

            for (BlockState state : states) {
                flags.add(getFlags(state));
                light.add(state.getLuminance() << 4 | state.getOpacity(emptyView, zeroPos));
                occlusionShapes.add(data.getShapeId(state.getCullingShape(emptyView, zeroPos)));
            }
            this.flags = deduplicate(flags);
            this.light = deduplicate(light);
            this.occlusionShapes = deduplicate(occlusionShapes);
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

            if (bs.hasSidedTransparency())
                flags |= 1 << 3;

            if (bs.hasRandomTicks())
                flags |= 1 << 4;

            if (bs.emitsRedstonePower())
                flags |= 1 << 5;

            if (!bs.getFluidState().isEmpty())
                flags |= 1 << 6; //IsImmerse

            if (bs.getBlock().hasDynamicBounds())
                flags |= 1 << 7;

            return flags;
        }

        @Override
        public boolean equals(Object obj)
        {
            return obj instanceof XBlockStates o && 
                   o.flags.equals(flags) && 
                   o.light.equals(light) &&
                   Objects.equals(o.occlusionShapes, occlusionShapes);
        }
        @Override
        public int hashCode()
        {
            return flags.hashCode();
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
            reg(Material.DECORATION         , "decoration");
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
            reg(Material.MOSS_BLOCK         , "coral");
            reg(Material.GOURD              , "vegetable");
            reg(Material.EGG                , "egg");
            reg(Material.CAKE               , "cake");
            reg(Material.AMETHYST           , "amethyst");
            reg(Material.POWDER_SNOW        , "powder_snow");
            reg(Material.SCULK              , "sculk");
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

        @Override
        public boolean equals(Object obj)
        {
            return obj instanceof XBlockProperty o && 
                   o.name.equals(name) && 
                   o.type.equals(type);
        }
        @Override
        public int hashCode()
        {
            return name.hashCode() * 31 + type.hashCode();
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

        public static String getEnumName(Class<? extends Enum<?>> type)
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
                
                //assert values order, expected to be [min, ..., max]
                int i = min;
                for (Integer val : prop.getValues()) {
                    if (val != i++) {
                        throw new IllegalStateException("IntProperty unordered");
                    }
                }
            }

            @Override
            public boolean equals(Object obj)
            {
                return obj instanceof XPropInt o && 
                       o.min == min && 
                       o.max == max &&
                       super.equals(obj);
            }
        }

        public static class XPropBool extends XBlockProperty
        {
            public XPropBool(BooleanProperty prop)
            {
                super(prop.getName(), "bool");

                if (prop.getValues().stream().findFirst().get() != true) {
                    //values are expected to be [true, false]
                    throw new IllegalStateException("BoolProperty unordered");
                }
            }

            @Override
            public boolean equals(Object obj)
            {
                return obj instanceof XPropBool && super.equals(obj);
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

            @Override
            public boolean equals(Object obj)
            {
                return obj instanceof XPropEnum o && 
                       o.enumType.equals(enumType) && 
                       o.values.equals(values) &&
                       super.equals(obj);
            }
        }
    }

}