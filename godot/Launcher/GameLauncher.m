#import <Cocoa/Cocoa.h>

@interface GameLauncher : NSObject <NSApplicationDelegate>
@property NSWindow *window;
@property NSTask *buildTask;
@property NSString *project;
@property NSString *logPath;
@property NSFileHandle *logHandle;
@end

@implementation GameLauncher
- (void)fail:(NSString *)message {
    [self.window close];
    NSAlert *alert = [[NSAlert alloc] init];
    alert.messageText = @"Не удалось запустить игру";
    alert.informativeText = message;
    [alert addButtonWithTitle:@"Закрыть"];
    [alert addButtonWithTitle:@"Показать журнал"];
    if ([alert runModal] == NSAlertSecondButtonReturn) {
        [[NSWorkspace sharedWorkspace] openURL:[NSURL fileURLWithPath:self.logPath]];
    }
    [NSApp terminate:nil];
}
- (NSDictionary *)environment {
    NSMutableDictionary *env = [[[NSProcessInfo processInfo] environment] mutableCopy];
    env[@"DOTNET_ROOT"] = [NSHomeDirectory() stringByAppendingPathComponent:@"Library/dotnet"];
    env[@"PATH"] = @"/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin";
    env[@"DOTNET_CLI_TELEMETRY_OPTOUT"] = @"1";
    return env;
}
- (void)applicationDidFinishLaunching:(NSNotification *)notification {
    self.project = [[NSBundle mainBundle] objectForInfoDictionaryKey:@"TradeCompanyProject"];
    NSString *logs = [NSHomeDirectory() stringByAppendingPathComponent:@"Library/Logs/TradeCompany"];
    [[NSFileManager defaultManager] createDirectoryAtPath:logs withIntermediateDirectories:YES attributes:nil error:nil];
    self.logPath = [logs stringByAppendingPathComponent:@"launcher.log"];
    [[NSFileManager defaultManager] createFileAtPath:self.logPath contents:[NSData data] attributes:nil];
    self.logHandle = [NSFileHandle fileHandleForWritingAtPath:self.logPath];
    if (![[NSFileManager defaultManager] fileExistsAtPath:[self.project stringByAppendingPathComponent:@"project.godot"]]) {
        [self fail:@"Папка проекта не найдена. Верните её на прежнее место или обновите приложение запуска."];
        return;
    }
    self.window = [[NSWindow alloc] initWithContentRect:NSMakeRect(0, 0, 440, 145)
        styleMask:NSWindowStyleMaskTitled backing:NSBackingStoreBuffered defer:NO];
    self.window.title = @"Торговая компания";
    self.window.releasedWhenClosed = NO;
    NSTextField *title = [NSTextField labelWithString:@"Подготовка торговой экспедиции…"];
    title.font = [NSFont systemFontOfSize:17 weight:NSFontWeightSemibold];
    title.frame = NSMakeRect(30, 88, 390, 25);
    [self.window.contentView addSubview:title];
    NSTextField *subtitle = [NSTextField labelWithString:@"Проверяем обновления проекта и открываем игру."];
    subtitle.frame = NSMakeRect(30, 54, 390, 22);
    subtitle.font = [NSFont systemFontOfSize:13];
    [self.window.contentView addSubview:subtitle];
    NSProgressIndicator *spinner = [[NSProgressIndicator alloc] initWithFrame:NSMakeRect(30, 24, 380, 14)];
    spinner.style = NSProgressIndicatorStyleBar;
    spinner.indeterminate = YES;
    [spinner startAnimation:nil];
    [self.window.contentView addSubview:spinner];
    [self.window center];
    [self.window makeKeyAndOrderFront:nil];
    [NSApp activateIgnoringOtherApps:YES];
    self.buildTask = [[NSTask alloc] init];
    self.buildTask.executableURL = [NSURL fileURLWithPath:[NSHomeDirectory() stringByAppendingPathComponent:@"Library/dotnet/dotnet"]];
    self.buildTask.currentDirectoryURL = [NSURL fileURLWithPath:self.project];
    self.buildTask.arguments = @[@"build", @"TradeCompanyGodot.csproj", @"--nologo", @"-p:UseSharedCompilation=false"];
    self.buildTask.environment = [self environment];
    self.buildTask.standardOutput = self.logHandle;
    self.buildTask.standardError = self.logHandle;
    __weak GameLauncher *weakSelf = self;
    self.buildTask.terminationHandler = ^(NSTask *task) {
        dispatch_async(dispatch_get_main_queue(), ^{
            GameLauncher *launcher = weakSelf;
            if (task.terminationStatus != 0) {
                [launcher fail:@"Ошибка сборки проекта. Подробности сохранены в журнале запуска."];
                return;
            }
            NSTask *game = [[NSTask alloc] init];
            game.executableURL = [NSURL fileURLWithPath:[NSHomeDirectory() stringByAppendingPathComponent:@"Applications/Godot_mono.app/Contents/MacOS/Godot"]];
            game.currentDirectoryURL = [NSURL fileURLWithPath:launcher.project];
            game.arguments = @[@"--path", launcher.project];
            game.environment = [launcher environment];
            game.standardOutput = launcher.logHandle;
            game.standardError = launcher.logHandle;
            NSError *error = nil;
            if (![game launchAndReturnError:&error]) {
                [launcher fail:[NSString stringWithFormat:@"Не удалось открыть Godot: %@", error.localizedDescription]];
                return;
            }
            [NSApp terminate:nil];
        });
    };
    NSError *error = nil;
    if (![self.buildTask launchAndReturnError:&error]) [self fail:error.localizedDescription];
}
@end

int main(int argc, const char *argv[]) {
    @autoreleasepool {
        NSApplication *app = [NSApplication sharedApplication];
        GameLauncher *delegate = [[GameLauncher alloc] init];
        app.delegate = delegate;
        [app setActivationPolicy:NSApplicationActivationPolicyRegular];
        [app run];
    }
    return 0;
}
