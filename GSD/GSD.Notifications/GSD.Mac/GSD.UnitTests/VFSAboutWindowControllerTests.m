#import <XCTest/XCTest.h>
#import "VFSMockAboutWindowController.h"
#import "VFSMockProductInfoFetcher.h"

NSString * const ExpectedGitVersionString = @"2.20.1.vfs.1.1.104.g2ab7360";
NSString * const ExpectedGSDVersionString = @"1.0.19116.1";

@interface VFSAboutWindowControllerTests : XCTestCase

@property (strong) VFSAboutWindowController *windowController;

@end

@implementation VFSAboutWindowControllerTests

- (void)setUp
{
    [super setUp];
    
    VFSMockProductInfoFetcher *mockProductInfoFetcher =
    [[VFSMockProductInfoFetcher alloc] initWithGitVersion:(NSString *) ExpectedGitVersionString
                                         GSDVersion:(NSString *) ExpectedGSDVersionString];
    
    self.windowController = [[VFSAboutWindowController alloc]
        initWithProductInfoFetcher:mockProductInfoFetcher];
}

- (void)tearDown
{
    [super tearDown];
}

- (void)testAboutWindowContainsGSDVersion
{
    XCTAssertEqual(self.windowController.GSDVersion,
                   ExpectedGSDVersionString,
                   @"Incorrect GSD version displayed in About box");
}

- (void)testAboutWindowContainsGitVersion
{
    XCTAssertEqual(self.windowController.gitVersion,
                   ExpectedGitVersionString,
                   @"Incorrect Git version displayed in About box");
}

@end
