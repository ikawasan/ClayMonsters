#import <AppKit/AppKit.h>
#import <CoreGraphics/CoreGraphics.h>
#import <QuartzCore/QuartzCore.h>

static uint64_t gSavedStyleMask;
static NSInteger gSavedLevel;
static BOOL gSavedOpaque;
static BOOL gSavedHasShadow;
static BOOL gConfigured;
static BOOL gDidPlace;
static NSColor *gSavedBackground;
static NSRect gSavedFrame;

static NSWindow *FindUnityWindow(void)
{
    NSApplication *app = [NSApplication sharedApplication];
    if (app.keyWindow != nil)
    {
        return app.keyWindow;
    }

    if (app.mainWindow != nil)
    {
        return app.mainWindow;
    }

    for (NSWindow *window in app.windows)
    {
        if (window.isVisible)
        {
            return window;
        }
    }

    return app.windows.firstObject;
}

static void MakeLayerTreeTransparent(CALayer *layer)
{
    if (layer == nil)
    {
        return;
    }

    layer.opaque = NO;
    layer.backgroundColor = CGColorGetConstantColor(kCGColorClear);
    if ([layer isKindOfClass:[CAMetalLayer class]])
    {
        ((CAMetalLayer *)layer).opaque = NO;
    }

    for (CALayer *child in layer.sublayers)
    {
        MakeLayerTreeTransparent(child);
    }
}

static void MakeViewTreeTransparent(NSView *view)
{
    if (view == nil)
    {
        return;
    }

    if (view.layer != nil)
    {
        MakeLayerTreeTransparent(view.layer);
    }

    for (NSView *child in view.subviews)
    {
        MakeViewTreeTransparent(child);
    }
}

static void SaveWindowState(NSWindow *window)
{
    if (gConfigured || window == nil)
    {
        return;
    }

    gSavedStyleMask = window.styleMask;
    gSavedLevel = window.level;
    gSavedOpaque = window.opaque;
    gSavedHasShadow = window.hasShadow;
    gSavedBackground = [window.backgroundColor copy];
    gSavedFrame = window.frame;
    gConfigured = YES;
}

static void ApplyChrome(NSWindow *window, int stayOnTop)
{
    window.styleMask = NSWindowStyleMaskBorderless;
    window.opaque = NO;
    window.backgroundColor = NSColor.clearColor;
    window.hasShadow = NO;
    window.hidesOnDeactivate = NO;
    window.ignoresMouseEvents = NO;
    if (stayOnTop)
    {
        window.level = NSStatusWindowLevel;
    }
    else
    {
        window.level = kCGDesktopWindowLevel;
    }

    window.collectionBehavior = NSWindowCollectionBehaviorCanJoinAllSpaces
        | NSWindowCollectionBehaviorFullScreenAuxiliary
        | NSWindowCollectionBehaviorIgnoresCycle;
    MakeViewTreeTransparent(window.contentView);
    if (stayOnTop)
    {
        [window orderFrontRegardless];
    }
    else
    {
        [window orderBack:nil];
    }
}

extern "C" int CMPet_ConfigurePetWindow(int width, int height, int stayOnTop)
{
    (void)width;
    (void)height;
    NSWindow *window = FindUnityWindow();
    if (window == nil)
    {
        return 0;
    }

    SaveWindowState(window);
    ApplyChrome(window, stayOnTop);
    if (!gDidPlace)
    {
        NSScreen *screen = window.screen ?: [NSScreen mainScreen];
        NSRect vis = screen.visibleFrame;
        NSSize size = window.frame.size;
        CGFloat x = vis.origin.x + ((vis.size.width - size.width) * 0.5);
        CGFloat y = vis.origin.y + ((vis.size.height - size.height) * 0.5);
        [window setFrameOrigin:NSMakePoint(x, y)];
        gDidPlace = YES;
    }

    return 1;
}

extern "C" void CMPet_SetScreenPosition(int x, int y, int width, int height)
{
    (void)width;
    (void)height;
    NSWindow *window = FindUnityWindow();
    if (window == nil)
    {
        return;
    }

    CGFloat scale = MAX(1.0, window.backingScaleFactor);
    CGFloat virtualLeft = CGFLOAT_MAX;
    CGFloat virtualTop = -CGFLOAT_MAX;
    for (NSScreen *screen in [NSScreen screens])
    {
        NSRect frame = screen.frame;
        virtualLeft = MIN(virtualLeft, NSMinX(frame));
        virtualTop = MAX(virtualTop, NSMaxY(frame));
    }

    NSSize size = window.frame.size;
    CGFloat macX = virtualLeft + (x / scale);
    CGFloat macY = virtualTop - (y / scale) - size.height;
    [window setFrameOrigin:NSMakePoint(macX, macY)];
}

extern "C" int CMPet_GetWorkingAreaCount(void)
{
    return (int)[NSScreen screens].count;
}

extern "C" int CMPet_GetWorkingArea(int index, int *outX, int *outY, int *outW, int *outH)
{
    if (outX == NULL || outY == NULL || outW == NULL || outH == NULL)
    {
        return 0;
    }

    NSArray<NSScreen *> *screens = [NSScreen screens];
    if (index < 0 || index >= (int)screens.count)
    {
        return 0;
    }

    CGFloat virtualLeft = CGFLOAT_MAX;
    CGFloat virtualTop = -CGFLOAT_MAX;
    for (NSScreen *screen in screens)
    {
        NSRect frame = screen.frame;
        virtualLeft = MIN(virtualLeft, NSMinX(frame));
        virtualTop = MAX(virtualTop, NSMaxY(frame));
    }

    NSScreen *screen = screens[index];
    NSRect vis = screen.visibleFrame;
    CGFloat scale = MAX(1.0, screen.backingScaleFactor);
    *outX = (int)lround((NSMinX(vis) - virtualLeft) * scale);
    *outY = (int)lround((virtualTop - NSMaxY(vis)) * scale);
    *outW = (int)lround(vis.size.width * scale);
    *outH = (int)lround(vis.size.height * scale);
    return 1;
}

extern "C" void CMPet_SetClickThrough(int enabled)
{
    NSWindow *window = FindUnityWindow();
    if (window == nil)
    {
        return;
    }

    window.ignoresMouseEvents = enabled ? YES : NO;
}

extern "C" void CMPet_RestoreWindow(void)
{
    NSWindow *window = FindUnityWindow();
    if (window == nil || !gConfigured)
    {
        return;
    }

    window.ignoresMouseEvents = NO;
    window.styleMask = gSavedStyleMask;
    window.level = gSavedLevel;
    window.opaque = gSavedOpaque;
    window.hasShadow = gSavedHasShadow;
    if (gSavedBackground != nil)
    {
        window.backgroundColor = gSavedBackground;
    }

    [window setFrame:gSavedFrame display:YES];
    gConfigured = NO;
    gDidPlace = NO;
}

@interface CMPetMenuTarget : NSObject
@property (nonatomic, assign) int selectedTag;
- (void)pick:(NSMenuItem *)sender;
@end

@implementation CMPetMenuTarget
- (void)pick:(NSMenuItem *)sender
{
    self.selectedTag = (int)sender.tag;
}
@end

extern "C" int CMPet_ShowContextMenu(const char *quitLabel, const char *launchLabel)
{
    NSString *quitTitle = quitLabel != NULL
        ? [NSString stringWithUTF8String:quitLabel]
        : @"Quit";
    NSString *launchTitle = launchLabel != NULL
        ? [NSString stringWithUTF8String:launchLabel]
        : @"Back";
    CMPetMenuTarget *target = [CMPetMenuTarget new];
    NSMenu *menu = [[NSMenu alloc] initWithTitle:@""];
    menu.autoenablesItems = NO;

    NSMenuItem *quitItem = [[NSMenuItem alloc] initWithTitle:quitTitle
                                                      action:@selector(pick:)
                                               keyEquivalent:@""];
    quitItem.target = target;
    quitItem.tag = 1;
    [menu addItem:quitItem];

    NSMenuItem *launchItem = [[NSMenuItem alloc] initWithTitle:launchTitle
                                                        action:@selector(pick:)
                                                 keyEquivalent:@""];
    launchItem.target = target;
    launchItem.tag = 2;
    [menu addItem:launchItem];

    [[NSApplication sharedApplication] activateIgnoringOtherApps:YES];
    NSPoint location = [NSEvent mouseLocation];
    BOOL picked = [menu popUpMenuPositioningItem:nil atLocation:location inView:nil];
    if (!picked)
    {
        return 0;
    }

    return target.selectedTag;
}
