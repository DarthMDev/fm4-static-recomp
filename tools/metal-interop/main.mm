// Copyright (c) 2026, FM4 Static Recomp contributors. BSD-3-Clause.
// Synthetic Vulkan -> Metal -> Vulkan -> Metal round trip. No texture readbacks.
#define VK_USE_PLATFORM_METAL_EXT
#import <Foundation/Foundation.h>
#import <Metal/Metal.h>
#include <vulkan/vulkan.h>
#include <algorithm>
#include <bit>
#include <iostream>
#include <stdexcept>
#include <vector>

static void Check(VkResult result, const char* operation) {
  if (result != VK_SUCCESS)
    throw std::runtime_error(std::string(operation) + ": " + std::to_string(result));
}
static void Require(bool result, const char* operation) {
  if (!result) throw std::runtime_error(operation);
}
struct Resources {
  VkInstance instance{};
  VkDevice device{};
  VkCommandPool pool{};
  VkSemaphore semaphore{};
  std::vector<VkImage> images;
  std::vector<VkDeviceMemory> allocations;
  ~Resources() {
    if (device) {
      vkDeviceWaitIdle(device);
      if (pool) vkDestroyCommandPool(device, pool, nullptr);
      if (semaphore) vkDestroySemaphore(device, semaphore, nullptr);
      for (auto image : images) vkDestroyImage(device, image, nullptr);
      for (auto memory : allocations) vkFreeMemory(device, memory, nullptr);
      vkDestroyDevice(device, nullptr);
    }
    if (instance) vkDestroyInstance(instance, nullptr);
  }
};

static void Exercise(unsigned size) {
  Resources r;
  VkApplicationInfo app{VK_STRUCTURE_TYPE_APPLICATION_INFO};
  app.pApplicationName = "FM4 Metal interop feasibility";
  app.apiVersion = VK_API_VERSION_1_2;
  // The portability enumeration extension belongs to the Vulkan loader and may
  // be absent when testing MoltenVK directly. Enable it only when advertised.
  uint32_t instance_count = 0;
  Check(vkEnumerateInstanceExtensionProperties(nullptr, &instance_count, nullptr), "instance extensions");
  std::vector<VkExtensionProperties> instance_available(instance_count);
  Check(vkEnumerateInstanceExtensionProperties(nullptr, &instance_count, instance_available.data()), "instance extensions");
  const bool portability = std::any_of(instance_available.begin(), instance_available.end(), [](const auto& e) {
    return std::string_view(e.extensionName) == VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME;
  });
  const char* instance_extensions[] = {VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME};
  VkExportMetalObjectCreateInfoEXT export_device{VK_STRUCTURE_TYPE_EXPORT_METAL_OBJECT_CREATE_INFO_EXT};
  export_device.exportObjectType = VK_EXPORT_METAL_OBJECT_TYPE_METAL_DEVICE_BIT_EXT;
  VkInstanceCreateInfo instance{VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO};
  instance.pNext = &export_device;
  instance.flags = portability ? VK_INSTANCE_CREATE_ENUMERATE_PORTABILITY_BIT_KHR : 0;
  instance.pApplicationInfo = &app;
  instance.enabledExtensionCount = portability ? 1 : 0;
  instance.ppEnabledExtensionNames = instance_extensions;
  Check(vkCreateInstance(&instance, nullptr, &r.instance), "instance");
  uint32_t count = 0;
  Check(vkEnumeratePhysicalDevices(r.instance, &count, nullptr), "device count");
  Require(count > 0, "no Vulkan device");
  std::vector<VkPhysicalDevice> devices(count);
  Check(vkEnumeratePhysicalDevices(r.instance, &count, devices.data()), "devices");
  const auto gpu = devices.front();
  VkPhysicalDeviceProperties properties{};
  vkGetPhysicalDeviceProperties(gpu, &properties);
  std::cout << "Device: " << properties.deviceName << ", size " << size << '\n';
  Check(vkEnumerateDeviceExtensionProperties(gpu, nullptr, &count, nullptr), "extension count");
  std::vector<VkExtensionProperties> extensions(count);
  Check(vkEnumerateDeviceExtensionProperties(gpu, nullptr, &count, extensions.data()), "extensions");
  auto has = [&](const char* name) {
    return std::any_of(extensions.begin(), extensions.end(), [&](const auto& e) {
      return std::string_view(e.extensionName) == name;
    });
  };
  Require(has(VK_EXT_METAL_OBJECTS_EXTENSION_NAME), "VK_EXT_metal_objects unsupported");
  std::vector<const char*> enabled{VK_EXT_METAL_OBJECTS_EXTENSION_NAME};
  if (has("VK_KHR_portability_subset")) enabled.push_back("VK_KHR_portability_subset");
  VkPhysicalDeviceTimelineSemaphoreFeatures timeline{VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_TIMELINE_SEMAPHORE_FEATURES};
  VkPhysicalDeviceFeatures2 features{VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2};
  features.pNext = &timeline;
  vkGetPhysicalDeviceFeatures2(gpu, &features);
  Require(timeline.timelineSemaphore, "timeline semaphores unsupported");
  vkGetPhysicalDeviceQueueFamilyProperties(gpu, &count, nullptr);
  std::vector<VkQueueFamilyProperties> families(count);
  vkGetPhysicalDeviceQueueFamilyProperties(gpu, &count, families.data());
  uint32_t family = 0;
  while (family < count && !(families[family].queueFlags & VK_QUEUE_GRAPHICS_BIT)) ++family;
  Require(family < count, "no graphics queue");
  float priority = 1.0f;
  VkDeviceQueueCreateInfo queue_info{VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO};
  queue_info.queueFamilyIndex = family;
  queue_info.queueCount = 1;
  queue_info.pQueuePriorities = &priority;
  VkDeviceCreateInfo device_info{VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO};
  device_info.pNext = &timeline;
  device_info.queueCreateInfoCount = 1;
  device_info.pQueueCreateInfos = &queue_info;
  device_info.enabledExtensionCount = uint32_t(enabled.size());
  device_info.ppEnabledExtensionNames = enabled.data();
  Check(vkCreateDevice(gpu, &device_info, nullptr, &r.device), "device");
  VkQueue queue{};
  vkGetDeviceQueue(r.device, family, 0, &queue);
  const auto export_objects = reinterpret_cast<PFN_vkExportMetalObjectsEXT>(
      vkGetDeviceProcAddr(r.device, "vkExportMetalObjectsEXT"));
  Require(export_objects, "missing vkExportMetalObjectsEXT");
  VkExportMetalDeviceInfoEXT metal_device{VK_STRUCTURE_TYPE_EXPORT_METAL_DEVICE_INFO_EXT};
  VkExportMetalObjectsInfoEXT objects{VK_STRUCTURE_TYPE_EXPORT_METAL_OBJECTS_INFO_EXT};
  objects.pNext = &metal_device;
  export_objects(r.device, &objects);
  id<MTLDevice> mtl = metal_device.mtlDevice;
  Require(mtl != nil, "device export failed");
  id<MTLCommandQueue> metal_queue = [mtl newCommandQueue];
  Require(metal_queue != nil, "Metal queue creation failed");

  VkPhysicalDeviceMemoryProperties memory_properties{};
  vkGetPhysicalDeviceMemoryProperties(gpu, &memory_properties);
  std::vector<id<MTLTexture>> textures;
  for (VkFormat format : {VK_FORMAT_R8G8B8A8_UNORM, VK_FORMAT_R32_SFLOAT,
                          VK_FORMAT_R16G16_SFLOAT, VK_FORMAT_R8G8B8A8_UNORM,
                          VK_FORMAT_R8G8B8A8_UNORM}) {
    VkExportMetalObjectCreateInfoEXT intent{VK_STRUCTURE_TYPE_EXPORT_METAL_OBJECT_CREATE_INFO_EXT};
    intent.exportObjectType = VK_EXPORT_METAL_OBJECT_TYPE_METAL_TEXTURE_BIT_EXT;
    VkImageCreateInfo image{VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO};
    image.pNext = &intent;
    image.imageType = VK_IMAGE_TYPE_2D;
    image.format = format;
    image.extent = {size, size, 1};
    image.mipLevels = image.arrayLayers = 1;
    image.samples = VK_SAMPLE_COUNT_1_BIT;
    image.tiling = VK_IMAGE_TILING_OPTIMAL;
    image.usage = VK_IMAGE_USAGE_TRANSFER_SRC_BIT | VK_IMAGE_USAGE_TRANSFER_DST_BIT |
                  VK_IMAGE_USAGE_SAMPLED_BIT | VK_IMAGE_USAGE_STORAGE_BIT;
    VkImage handle{};
    Check(vkCreateImage(r.device, &image, nullptr, &handle), "image");
    r.images.push_back(handle);
    VkMemoryRequirements requirements{};
    vkGetImageMemoryRequirements(r.device, handle, &requirements);
    uint32_t type = 0;
    while (type < memory_properties.memoryTypeCount &&
           (!(requirements.memoryTypeBits & (1u << type)) ||
            !(memory_properties.memoryTypes[type].propertyFlags & VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT))) ++type;
    Require(type < memory_properties.memoryTypeCount, "no device-local texture memory");
    VkMemoryAllocateInfo allocation{VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO};
    allocation.allocationSize = requirements.size;
    allocation.memoryTypeIndex = type;
    VkDeviceMemory memory{};
    Check(vkAllocateMemory(r.device, &allocation, nullptr, &memory), "allocate");
    r.allocations.push_back(memory);
    Check(vkBindImageMemory(r.device, handle, memory, 0), "bind");
    VkExportMetalTextureInfoEXT texture{VK_STRUCTURE_TYPE_EXPORT_METAL_TEXTURE_INFO_EXT};
    texture.image = handle;
    texture.plane = VK_IMAGE_ASPECT_COLOR_BIT;
    objects.pNext = &texture;
    export_objects(r.device, &objects);
    Require(texture.mtlTexture != nil, "texture export failed");
    textures.push_back(texture.mtlTexture);
  }
  VkExportMetalObjectCreateInfoEXT event_intent{VK_STRUCTURE_TYPE_EXPORT_METAL_OBJECT_CREATE_INFO_EXT};
  event_intent.exportObjectType = VK_EXPORT_METAL_OBJECT_TYPE_METAL_SHARED_EVENT_BIT_EXT;
  VkSemaphoreTypeCreateInfo semaphore_type{VK_STRUCTURE_TYPE_SEMAPHORE_TYPE_CREATE_INFO};
  semaphore_type.pNext = &event_intent;
  semaphore_type.semaphoreType = VK_SEMAPHORE_TYPE_TIMELINE;
  VkSemaphoreCreateInfo semaphore{VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO};
  semaphore.pNext = &semaphore_type;
  Check(vkCreateSemaphore(r.device, &semaphore, nullptr, &r.semaphore), "semaphore");
  VkExportMetalSharedEventInfoEXT shared{VK_STRUCTURE_TYPE_EXPORT_METAL_SHARED_EVENT_INFO_EXT};
  shared.semaphore = r.semaphore;
  objects.pNext = &shared;
  export_objects(r.device, &objects);
  id<MTLSharedEvent> event = shared.mtlSharedEvent;
  Require(event != nil, "shared event export failed");

  NSError* error = nil;
  NSString* shader = @R"(
#include <metal_stdlib>
using namespace metal;
kernel void reconstruct(texture2d<float, access::read> color [[texture(0)]],
                        texture2d<float, access::read> depth [[texture(1)]],
                        texture2d<float, access::read> motion [[texture(2)]],
                        texture2d<float, access::write> output [[texture(3)]],
                        uint2 p [[thread_position_in_grid]]) {
  if (p.x >= output.get_width() || p.y >= output.get_height()) return;
  bool ok = all(abs(color.read(p) - float4(0.25, 0.5, 0.75, 1)) < 0.005) &&
            abs(depth.read(p).x - 0.5) < 0.001 &&
            all(abs(motion.read(p).xy - float2(0.125, -0.25)) < 0.001);
  output.write(ok ? float4(0, 1, 0, 1) : float4(1, 0, 0, 1), p);
}
kernel void verify(texture2d<float, access::read> image [[texture(0)]],
                   device atomic_uint* failures [[buffer(0)]],
                   uint2 p [[thread_position_in_grid]]) {
  if (p.x >= image.get_width() || p.y >= image.get_height()) return;
  if (any(abs(image.read(p) - float4(0, 1, 0, 1)) > 0.005))
    atomic_fetch_add_explicit(failures, 1u, memory_order_relaxed);
}
)";
  id<MTLLibrary> library = [mtl newLibraryWithSource:shader options:nil error:&error];
  if (!library) throw std::runtime_error(error.localizedDescription.UTF8String);
  id<MTLComputePipelineState> reconstruct = [mtl newComputePipelineStateWithFunction:
      [library newFunctionWithName:@"reconstruct"] error:&error];
  id<MTLComputePipelineState> verify = [mtl newComputePipelineStateWithFunction:
      [library newFunctionWithName:@"verify"] error:&error];
  Require(reconstruct != nil && verify != nil, "Metal pipeline creation failed");
  id<MTLBuffer> status = [mtl newBufferWithLength:sizeof(uint32_t) options:MTLResourceStorageModeShared];
  Require(status != nil, "diagnostic allocation failed");
  *static_cast<uint32_t*>(status.contents) = 0;
  VkCommandPoolCreateInfo pool{VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO};
  pool.queueFamilyIndex = family;
  Check(vkCreateCommandPool(r.device, &pool, nullptr, &r.pool), "command pool");
  VkCommandBufferAllocateInfo allocate{VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO};
  allocate.commandPool = r.pool;
  allocate.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
  allocate.commandBufferCount = 2;
  VkCommandBuffer commands[2]{};
  Check(vkAllocateCommandBuffers(r.device, &allocate, commands), "commands");
  VkCommandBufferBeginInfo begin{VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO};
  Check(vkBeginCommandBuffer(commands[0], &begin), "begin clear");
  VkImageSubresourceRange range{VK_IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1};
  for (auto image : r.images) {
    VkImageMemoryBarrier barrier{VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER};
    barrier.oldLayout = VK_IMAGE_LAYOUT_UNDEFINED;
    barrier.newLayout = VK_IMAGE_LAYOUT_GENERAL;
    barrier.srcQueueFamilyIndex = barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
    barrier.image = image;
    barrier.subresourceRange = range;
    barrier.dstAccessMask = VK_ACCESS_MEMORY_WRITE_BIT | VK_ACCESS_MEMORY_READ_BIT;
    vkCmdPipelineBarrier(commands[0], VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT,
                         VK_PIPELINE_STAGE_ALL_COMMANDS_BIT, 0, 0, nullptr, 0, nullptr, 1, &barrier);
  }
  VkClearColorValue colors[3] = {{{0.25f, 0.5f, 0.75f, 1.0f}},
                                {{0.5f, 0, 0, 0}}, {{0.125f, -0.25f, 0, 0}}};
  for (unsigned i = 0; i < 3; ++i)
    vkCmdClearColorImage(commands[0], r.images[i], VK_IMAGE_LAYOUT_GENERAL, &colors[i], 1, &range);
  Check(vkEndCommandBuffer(commands[0]), "end clear");
  uint64_t value = 1;
  VkTimelineSemaphoreSubmitInfo timeline_submit{VK_STRUCTURE_TYPE_TIMELINE_SEMAPHORE_SUBMIT_INFO};
  timeline_submit.signalSemaphoreValueCount = 1;
  timeline_submit.pSignalSemaphoreValues = &value;
  VkSubmitInfo submit{VK_STRUCTURE_TYPE_SUBMIT_INFO};
  submit.pNext = &timeline_submit;
  submit.commandBufferCount = 1;
  submit.pCommandBuffers = &commands[0];
  submit.signalSemaphoreCount = 1;
  submit.pSignalSemaphores = &r.semaphore;
  Check(vkQueueSubmit(queue, 1, &submit, VK_NULL_HANDLE), "Vulkan write and signal 1");
  id<MTLCommandBuffer> metal = [metal_queue commandBuffer];
  [metal encodeWaitForEvent:event value:1];
  id<MTLComputeCommandEncoder> encoder = [metal computeCommandEncoder];
  [encoder setComputePipelineState:reconstruct];
  for (unsigned i = 0; i < 4; ++i) [encoder setTexture:textures[i] atIndex:i];
  const auto grid = MTLSizeMake(size, size, 1), group = MTLSizeMake(8, 8, 1);
  [encoder dispatchThreads:grid threadsPerThreadgroup:group];
  [encoder endEncoding];
  [metal encodeSignalEvent:event value:2];
  [metal commit];

  Check(vkBeginCommandBuffer(commands[1], &begin), "begin copy");
  VkImageCopy copy{};
  copy.srcSubresource = copy.dstSubresource = {VK_IMAGE_ASPECT_COLOR_BIT, 0, 0, 1};
  copy.extent = {size, size, 1};
  vkCmdCopyImage(commands[1], r.images[3], VK_IMAGE_LAYOUT_GENERAL,
                 r.images[4], VK_IMAGE_LAYOUT_GENERAL, 1, &copy);
  Check(vkEndCommandBuffer(commands[1]), "end copy");
  const uint64_t wait = 2;
  value = 3;
  timeline_submit.waitSemaphoreValueCount = 1;
  timeline_submit.pWaitSemaphoreValues = &wait;
  VkPipelineStageFlags stage = VK_PIPELINE_STAGE_ALL_COMMANDS_BIT;
  submit.pCommandBuffers = &commands[1];
  submit.waitSemaphoreCount = 1;
  submit.pWaitSemaphores = &r.semaphore;
  submit.pWaitDstStageMask = &stage;
  Check(vkQueueSubmit(queue, 1, &submit, VK_NULL_HANDLE), "Vulkan wait 2, copy, signal 3");
  id<MTLCommandBuffer> final = [metal_queue commandBuffer];
  [final encodeWaitForEvent:event value:3];
  encoder = [final computeCommandEncoder];
  [encoder setComputePipelineState:verify];
  [encoder setTexture:textures[4] atIndex:0];
  [encoder setBuffer:status offset:0 atIndex:0];
  [encoder dispatchThreads:grid threadsPerThreadgroup:group];
  [encoder endEncoding];
  [final commit];
  // Only the one-shot test waits on the CPU; no CPU texture copies are performed.
  [final waitUntilCompleted];
  Require(metal.status == MTLCommandBufferStatusCompleted, "Metal reconstruction command failed");
  Require(final.status == MTLCommandBufferStatusCompleted, "Metal verification command failed");
  Require(*static_cast<uint32_t*>(status.contents) == 0, "GPU detected incorrect shared texture data");
  std::cout << "PASS: RGBA8 color/output, R32F depth, RG16F motion; GPU event values 1 -> 2 -> 3; zero texture readbacks\n";
}
int main() {
  @autoreleasepool {
    try {
      Exercise(64);
      Exercise(96);  // Destroy and recreate resources at a different size.
      return 0;
    } catch (const std::exception& error) {
      std::cerr << "FAIL: " << error.what() << '\n';
      return 1;
    }
  }
}
